using Godot;
using System.Collections.Generic;

/// <summary>
/// 装备管理器，管理所有单位的装备生命周期。
/// 一个单位只能装备一件装备；再装备时先完整移除旧装备（替换语义）。
/// 属性加成可逆：Equip 施加加成，RemoveEquipment 按相同数值减回。
/// </summary>
public partial class EquipmentManager : Node
{
    public static EquipmentManager Instance { get; private set; }

    /// <summary>单位 → 其当前装备（一单位一件）</summary>
    private Dictionary<Unit, Equipment> _equipments = new();

    /// <summary>装备施加事件（View 层订阅，创建 EquipmentView）</summary>
    public event System.Action<Unit, Equipment> EquipmentApplied;

    /// <summary>装备移除事件（View 层订阅，销毁 EquipmentView）</summary>
    public event System.Action<Unit, Equipment> EquipmentRemoved;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void Init() { }

    // ======================================================================
    // 装备
    // ======================================================================

    /// <summary>
    /// 给目标单位装备。若目标已有装备 → 先完整移除旧装备（属性还原+取消被动），再装新。
    /// </summary>
    public void Equip(Unit target, EquipmentData data, Unit sourceUnit)
    {
        if (target == null || data == null) return;
        if (!target.IsAlive || target.IsDead)
        {
            GD.Print($"[EquipmentManager] 目标无效或已死亡，无法装备: {data.EquipmentName}");
            return;
        }

        // 替换语义：先完整移除旧装备
        if (_equipments.TryGetValue(target, out var old))
            RemoveEquipment(target, old);

        var equip = new Equipment(data, sourceUnit);
        _equipments[target] = equip;

        ApplyBonuses(target, data);

        // 注册被动效果（带 tag 以便移除时单独清理）
        if (data.PassiveEffects != null && data.PassiveEffects.Length > 0)
        {
            string tag = $"equip_{data.EquipmentID}";
            EventBus.Instance?.Subscribe(target, data.PassiveEffects, tag);
        }

        target.UpdateUnit();

        // 通知 View 层创建装备图标（事件驱动）
        EquipmentApplied?.Invoke(target, equip);

        GD.Print($"[EquipmentManager] 装备: {data.EquipmentName} 于 {target.UnitData?.UnitName} " +
                 $"ATK+{data.AttackBonus} MaxHP+{data.MaxHealthBonus}");
    }

    // ======================================================================
    // 移除装备
    // ======================================================================

    /// <summary>
    /// 移除指定单位的装备：
    ///   1) 还原属性加成（可逆核心）
    ///   2) 取消被动效果订阅
    ///   3) 触发移除事件
    /// </summary>
    public void RemoveEquipment(Unit target, Equipment equip)
    {
        if (equip == null || equip.IsExpired) return;
        equip.IsExpired = true;

        var data = equip.Data;
        if (data != null)
        {
            RemoveBonuses(target, data);

            string tag = $"equip_{data.EquipmentID}";
            EventBus.Instance?.UnsubscribeByTag(tag);
        }

        target.UpdateUnit();

        // 通知 View 层销毁装备图标（事件驱动）
        EquipmentRemoved?.Invoke(target, equip);

        // 清理 EquipmentManager 记录（仅当仍是当前装备时移除，防止替换竞态）
        if (_equipments.TryGetValue(target, out var current) && current == equip)
            _equipments.Remove(target);

        GD.Print($"[EquipmentManager] 移除装备: {data?.EquipmentName} 于 {target.UnitData?.UnitName}");
    }

    /// <summary>
    /// 移除单位的所有装备（单位死亡时调用）。
    /// 还原属性 + 取消被动，事件驱动视图销毁。
    /// </summary>
    public void RemoveAllEquipments(Unit unit)
    {
        if (!_equipments.TryGetValue(unit, out var equip)) return;
        if (equip.IsExpired) return;

        equip.IsExpired = true;

        var data = equip.Data;
        if (data != null)
        {
            RemoveBonuses(unit, data);

            string tag = $"equip_{data.EquipmentID}";
            EventBus.Instance?.UnsubscribeByTag(tag);
        }

        // 通知 View 层销毁装备图标（事件驱动）
        EquipmentRemoved?.Invoke(unit, equip);

        _equipments.Remove(unit);
        GD.Print($"[EquipmentManager] 清除单位装备: {unit.UnitData?.UnitName}");
    }

    // ======================================================================
    // 属性加成（走 GameAction，可逆）
    // ======================================================================

    /// <summary>
    /// 汇总装备施加/移除要执行的动作：
    ///   1) 五个 bonus 字段中非 0 的转换为 ModifyStatAction（先执行，为 0 的忽略）
    ///   2) OnApplyActions 追加执行
    /// 施加时 Execute、移除时 Revert，保证可逆。
    /// </summary>
    private static GameAction[] ResolveActions(EquipmentData data)
    {
        var actions = new List<GameAction>();
        if (data.AttackBonus != 0)
            actions.Add(new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = data.AttackBonus });
        if (data.MaxHealthBonus != 0)
            actions.Add(new ModifyStatAction { TargetStat = ModifyStatType.MaxHP, Value = data.MaxHealthBonus });
        if (data.AttackDistanceBonus != 0)
            actions.Add(new ModifyStatAction { TargetStat = ModifyStatType.AttackDistance, Value = data.AttackDistanceBonus });
        if (data.StaminaBonus != 0)
            actions.Add(new ModifyStatAction { TargetStat = ModifyStatType.Stamina, Value = data.StaminaBonus });
        if (data.ActionPointBonus != 0)
            actions.Add(new ModifyStatAction { TargetStat = ModifyStatType.ActionPoints, Value = data.ActionPointBonus });
        if (data.OnApplyActions != null)
            foreach (var a in data.OnApplyActions)
                if (a != null) actions.Add(a);
        return actions.ToArray();
    }

    /// <summary>施加装备加成：执行 bonus 转换的 ModifyStatAction + OnApplyActions</summary>
    private static void ApplyBonuses(Unit target, EquipmentData data)
    {
        var ctx = new Context { TargetUnit = target };
        foreach (var action in ResolveActions(data))
            action.Execute(ctx);
    }

    /// <summary>还原装备加成：与施加同序 Revert（仿 BuffManager.RemoveBuff）</summary>
    private static void RemoveBonuses(Unit target, EquipmentData data)
    {
        var ctx = new Context { TargetUnit = target };
        foreach (var action in ResolveActions(data))
            action.Revert(ctx);
    }

    // ======================================================================
    // 查询
    // ======================================================================

    /// <summary>获取单位的当前装备，没有则返回 null</summary>
    public Equipment GetEquipment(Unit unit)
    {
        return _equipments.TryGetValue(unit, out var equip) ? equip : null;
    }

    /// <summary>检查单位是否已装备</summary>
    public bool HasEquipment(Unit unit)
    {
        return _equipments.ContainsKey(unit);
    }
}
