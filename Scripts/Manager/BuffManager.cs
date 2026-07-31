using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Buff 管理器，管理场上所有单位的 Buff 生命周期。
/// 负责施加、叠层/刷新、回合倒计时、到期移除和死亡清理。
/// </summary>
public partial class BuffManager : Node
{
    public static BuffManager Instance { get; private set; }

    /// <summary>单位 → 其活跃 Buff 列表</summary>
    private Dictionary<Unit, List<Buff>> _activeBuffs = new();

    /// <summary>Buff 施加事件（View 层订阅，创建 BuffView 图标）</summary>
    public event System.Action<Unit, Buff> BuffApplied;

    /// <summary>Buff 移除事件（View 层订阅，销毁 BuffView 图标）</summary>
    public event System.Action<Unit, Buff> BuffRemoved;

    public override void _Ready()
    {
        Instance = this;
    }

    public void Init() { }

    // ======================================================================
    // 施加 Buff
    // ======================================================================

    /// <summary>
    /// 对目标单位施加 Buff。
    /// 若目标已有同 ID Buff → 叠层（不超过 MaxStack）+ 刷新持续时间。
    /// 若没有 → 新建并执行 OnApplyActions + 订阅被动效果。
    /// </summary>
    public void ApplyBuff(Unit target, BuffData buffData, Unit sourceUnit, int initialStacks = 1)
    {
        GD.Print($"[BuffManager] ApplyBuff: target={target?.UnitData?.UnitName} buff={buffData?.BuffName} " +
                 $"duration={buffData?.Duration} maxStack={buffData?.MaxStack} initialStacks={initialStacks} " +
                 $"actionsCount={buffData?.OnApplyActions?.Length}");
        if (target == null || buffData == null) return;
        if (!target.IsAlive || target.IsDead) return;
        if (buffData.MaxStack == 0)
        {
            GD.PrintErr($"[BuffManager] MaxStack 为 0，无法施加 Buff: {buffData.BuffName}");
            return;
        }

        // ── 检查是否已有同 ID Buff ──────────────────────────────
        if (_activeBuffs.TryGetValue(target, out var buffList))
        {
            var existing = buffList.Find(b => b.Data.BuffID == buffData.BuffID);
            if (existing != null)
            {
                // 先按旧层数还原，再按新层数重新施加
                {
                    var ctx = new Context { TargetUnit = target };
                    for (int i = 0; i < existing.StackCount; i++)
                        foreach (var action in buffData.OnApplyActions)
                            action.Revert(ctx);
                }

                // 叠层：-1 = 无限叠，>0 不超过上限，0 不应出现
                // 此处用 initialStacks（例如卡牌定义的初始层数），而非固定 +1
                if (buffData.MaxStack == -1)
                    existing.StackCount += initialStacks;
                else if (buffData.MaxStack > 0)
                    existing.StackCount = System.Math.Min(existing.StackCount + initialStacks, buffData.MaxStack);
                existing.RemainingTurns = buffData.Duration;

                {
                    var ctx = new Context { TargetUnit = target, SourceUnit = sourceUnit };
                    for (int i = 0; i < existing.StackCount; i++)
                        foreach (var action in buffData.OnApplyActions)
                            action.Execute(ctx);
                }

                GD.Print($"[BuffManager] 刷新+叠层: {buffData.BuffName} ×{existing.StackCount} " +
                         $"剩余{existing.RemainingTurns}回合 目标={target.UnitData?.UnitName}");
                return;
            }
        }

        // ── 新建 Buff ──────────────────────────────────────────
        var buff = new Buff(buffData, sourceUnit);
        buff.StackCount = initialStacks;
        if (!_activeBuffs.ContainsKey(target))
            _activeBuffs[target] = new List<Buff>();
        _activeBuffs[target].Add(buff);

        // 执行施加动作（按层数倍数执行）
        {
            var ctx = new Context { TargetUnit = target, SourceUnit = sourceUnit };
            for (int i = 0; i < initialStacks; i++)
                foreach (var action in buffData.OnApplyActions)
                    action.Execute(ctx);
        }

        // 注册被动效果（带 tag 以便到期单独清理）
        if (buffData.PassiveEffects != null && buffData.PassiveEffects.Length > 0)
        {
            string tag = $"buff_{buffData.BuffID}";
            EventBus.Instance?.Subscribe(target, buffData.PassiveEffects, tag);
        }

        // 触发事件
        target.UpdateUnit();
        EventBus.Instance?.Fire(EventType.OnBuffApplied,
            new Context { TargetUnit = target }, subject: target);

        // 通知 View 层创建 Buff 图标（事件驱动）
        BuffApplied?.Invoke(target, buff);

        GD.Print($"[BuffManager] 施加: {buffData.BuffName} 于 {target.UnitData?.UnitName} " +
                 $"持续{buffData.Duration}回合 叠层上限{buffData.MaxStack}");
    }

    // ======================================================================
    // 移除 Buff
    // ======================================================================

    /// <summary>
    /// 移除指定单位的指定 Buff：
    ///   1) 还原属性修改（Revert OnApplyActions）
    ///   2) 取消被动效果订阅
    ///   3) 执行 OnExpireActions
    ///   4) 触发移除事件
    /// </summary>
    public void RemoveBuff(Unit target, Buff buff)
    {
        if (buff == null || buff.IsExpired) return;
        buff.IsExpired = true;

        // ── 还原属性修改 ──────────────────────────────────────────
        if (buff.Data.OnApplyActions != null)
        {
            var ctx = new Context { TargetUnit = target };
            for (int i = 0; i < buff.StackCount; i++)
                foreach (var action in buff.Data.OnApplyActions)
                    action.Revert(ctx);
        }

        // ── 取消被动效果订阅 ──────────────────────────────────────
        string tag = $"buff_{buff.Data.BuffID}";
        EventBus.Instance?.UnsubscribeByTag(tag);

        // ── 执行到期动作 ──────────────────────────────────────────
        if (buff.Data.OnExpireActions != null && buff.Data.OnExpireActions.Length > 0)
        {
            var ctx = new Context { TargetUnit = target, SourceUnit = buff.SourceUnit };
            foreach (var action in buff.Data.OnExpireActions)
                action.Execute(ctx);
        }

        // ── 触发事件 ──────────────────────────────────────────────
        target.UpdateUnit();
        EventBus.Instance?.Fire(EventType.OnBuffRemoved,
            new Context { TargetUnit = target }, subject: target);

        // ── 通知 View 层销毁 Buff 图标（事件驱动）──────────────────
        BuffRemoved?.Invoke(target, buff);

        // ── 清理 BuffManager 记录 ────────────────────────────────
        if (_activeBuffs.TryGetValue(target, out var buffList))
        {
            buffList.Remove(buff);
            if (buffList.Count == 0)
                _activeBuffs.Remove(target);
        }

        GD.Print($"[BuffManager] 移除: {buff.Data.BuffName} 于 {target.UnitData?.UnitName}");
    }

    /// <summary>
    /// 驱散骨架：按 BuffData 查找并移除指定单位的该 Buff（所有层）。
    /// 供未来驱散卡牌/技能调用。
    /// </summary>
    public void RemoveBuffByData(Unit target, BuffData buffData)
    {
        if (target == null || buffData == null) return;
        if (!_activeBuffs.TryGetValue(target, out var buffList)) return;

        var toRemove = buffList.FindAll(b => b.Data.BuffID == buffData.BuffID);
        foreach (var buff in toRemove)
            RemoveBuff(target, buff);
    }

    /// <summary>
    /// 移除单位的所有 Buff（单位死亡时调用）。
    /// 还原属性但**不**执行 OnExpireActions（死亡场景下避免副作用）。
    /// </summary>
    public void RemoveAllBuffs(Unit unit)
    {
        if (!_activeBuffs.TryGetValue(unit, out var buffs)) return;

        // 快照遍历
        foreach (var buff in buffs.ToList())
        {
            buff.IsExpired = true;

            // 还原属性修改
            if (buff.Data.OnApplyActions != null)
            {
                var ctx = new Context { TargetUnit = unit };
                for (int i = 0; i < buff.StackCount; i++)
                    foreach (var action in buff.Data.OnApplyActions)
                        action.Revert(ctx);
            }

            // 取消被动效果订阅
            string tag = $"buff_{buff.Data.BuffID}";
            EventBus.Instance?.UnsubscribeByTag(tag);

            // 通知 View 层销毁 Buff 图标（事件驱动）
            BuffRemoved?.Invoke(unit, buff);

            // 不执行 OnExpireActions
        }

        _activeBuffs.Remove(unit);
        GD.Print($"[BuffManager] 清除单位所有 Buff: {unit.UnitData?.UnitName}");
    }

    // ======================================================================
    // 回合倒计时
    // ======================================================================

    /// <summary>
    /// 每回合结束时调用：
    ///   1) 所有 Buff 的 RemainingTurns-1
    ///   2) 执行 OnRoundEndActions
    ///   3) 归零的 Buff 调用 RemoveBuff（含 OnExpireActions）
    /// </summary>
    public void TickAllBuffs()
    {
        var toRemove = new List<(Unit target, Buff buff)>();

        foreach (var kv in _activeBuffs)
        {
            var target = kv.Key;
            if (!target.IsAlive || target.IsDead)
            {
                // 安全清理：UnitManager 应该已经处理了，但确保万无一失
                foreach (var b in kv.Value.ToList())
                    toRemove.Add((target, b));
                continue;
            }

            foreach (var buff in kv.Value)
            {
                bool expired = false;

                // Duration = 0: 直接移除，不倒计时
                if (buff.Data.Duration == 0)
                {
                    expired = true;
                }
                // Duration > 0: 正常倒计时，最小减到 0
                else if (buff.Data.Duration > 0)
                {
                    if (buff.RemainingTurns > 0)
                        buff.RemainingTurns--;
                    if (buff.RemainingTurns <= 0)
                        expired = true;
                }
                // Duration < 0 (-1): 永久，跳过

                // 叠层归零也视为到期
                if (buff.StackCount <= 0)
                    expired = true;

                // 执行回合结束动作（即使是归零的这回合也执行）
                if (buff.Data.OnRoundEndActions != null && buff.Data.OnRoundEndActions.Length > 0)
                {
                    var ctx = new Context { TargetUnit = target, SourceUnit = buff.SourceUnit };
                    foreach (var action in buff.Data.OnRoundEndActions)
                        action.Execute(ctx);
                }

                if (expired)
                    toRemove.Add((target, buff));
            }
        }

        foreach (var (target, buff) in toRemove)
            RemoveBuff(target, buff);
    }

    // ======================================================================
    // 查询
    // ======================================================================

    /// <summary>获取单位的所有活跃 Buff</summary>
    public List<Buff> GetBuffs(Unit unit)
    {
        return _activeBuffs.TryGetValue(unit, out var buffs)
            ? new List<Buff>(buffs) : new List<Buff>();
    }

    /// <summary>检查单位是否拥有指定 ID 的 Buff</summary>
    public bool HasBuff(Unit unit, string buffID)
    {
        return _activeBuffs.TryGetValue(unit, out var buffs)
            && buffs.Exists(b => b.Data.BuffID == buffID);
    }

    /// <summary>获取单位上指定 ID 的 Buff（含叠层信息）</summary>
    public Buff GetBuff(Unit unit, string buffID)
    {
        if (_activeBuffs.TryGetValue(unit, out var buffs))
            return buffs.Find(b => b.Data.BuffID == buffID);
        return null;
    }
}
