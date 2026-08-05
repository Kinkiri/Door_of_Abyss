using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 事件总线，管理被动效果的订阅和触发。
/// 各 Manager 在合适的时机调用 Fire()，EventBus 负责通知对应的被动效果订阅者。
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    /// <summary>按事件类型分组</summary>
    private Dictionary<EventType, List<Subscription>> _subscriptions = new();

    /// <summary>触发计数追踪：按订阅条目独立计数（同单位同事件多被动互不共享）</summary>
    private Dictionary<Subscription, (int max, int current)> _triggerCounts = new();

    private class Subscription
    {
        /// <summary>订阅者：Unit（单位被动）或 Card（手牌被动）</summary>
        public object Owner;
        public GameAction[] Actions;
        public PassiveTarget PassiveTarget;
        public TargetFilter TargetFilter;
        public int MaxTriggerCount;
        public Condition[] Conditions;
        public string Tag;
    }

    public override void _Ready()
    {
        Instance = this;
    }

    public void Init() { }

    /// <summary>
    /// 注册被动效果（无标签）。owner 为 Unit（单位原生被动）或 Card（手牌被动）。
    /// </summary>
    public void Subscribe(object owner, EffectData[] effects)
    {
        Subscribe(owner, effects, null);
    }

    /// <summary>
    /// 注册被动效果（带标签，用于 Buff 等临时效果）
    /// </summary>
    /// <param name="tag">订阅标签，Buff 到期时通过此标签单独清理</param>
    public void Subscribe(object owner, EffectData[] effects, string tag)
    {
        if (effects == null) return;

        GD.Print($"[EventBus] === 订阅: {GetOwnerName(owner)} tag={tag ?? "null"} ===");

        foreach (var effect in effects)
        {
            if (effect?.Actions == null) continue;

            if (!_subscriptions.TryGetValue(effect.TriggerEvent, out var list))
            {
                list = new List<Subscription>();
                _subscriptions[effect.TriggerEvent] = list;
            }

            var sub = new Subscription
            {
                Owner = owner,
                Actions = effect.Actions,
                PassiveTarget = effect.Target,
                TargetFilter = TargetFilter.CombineAnd(effect.TargetFilters),
                MaxTriggerCount = effect.MaxTriggerCount,
                Conditions = effect.Conditions,
                Tag = tag,
            };
            list.Add(sub);

            // 初始化触发计数（按订阅条目独立计数）
            if (effect.MaxTriggerCount > 0)
            {
                _triggerCounts[sub] = (effect.MaxTriggerCount, effect.MaxTriggerCount);
            }

            GD.Print($"[EventBus]   注册 {effect.TriggerEvent} target={effect.Target} " +
                     $"targetFilter={TargetFilter.CombineAnd(effect.TargetFilters)?.GetType().Name ?? "null"} maxTrigger={effect.MaxTriggerCount} " +
                     $"action={string.Join(",", Array.ConvertAll(effect.Actions, a => a?.GetType().Name ?? "null"))}");
        }
    }

    /// <summary>
    /// 移除订阅者的所有被动效果订阅（单位销毁 / 卡牌打出、弃牌时调用）
    /// </summary>
    public void Unsubscribe(object owner)
    {
        GD.Print($"[EventBus] === 取消订阅: {GetOwnerName(owner)} ===");
        int removed = 0;
        foreach (var kv in _subscriptions)
        {
            removed += kv.Value.RemoveAll(s => s.Owner == owner);
        }
        // 清理触发计数（按订阅条目，owner 匹配即删）
        var keys = _triggerCounts.Keys.Where(k => k.Owner == owner).ToList();
        foreach (var key in keys)
            _triggerCounts.Remove(key);
        GD.Print($"[EventBus]   移除 {removed} 条订阅");
    }

    /// <summary>
    /// 按标签取消订阅（用于 Buff 到期时单独清理）。
    /// 仅移除该标签的订阅，不影响单位原生被动。
    /// </summary>
    public void UnsubscribeByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        var eventTypesWithRemovals = new List<EventType>();
        int removed = 0;

        foreach (var kv in _subscriptions)
        {
            int before = kv.Value.Count;
            kv.Value.RemoveAll(s => s.Tag == tag);
            int after = kv.Value.Count;
            if (before != after)
            {
                removed += before - after;
                if (after == 0)
                    eventTypesWithRemovals.Add(kv.Key);
            }
        }

        // 清理空列表的事件类型
        foreach (var et in eventTypesWithRemovals)
        {
            if (_subscriptions[et].Count == 0)
                _subscriptions.Remove(et);
        }

        GD.Print($"[EventBus] 按标签取消订阅: tag={tag} 移除 {removed} 条");
    }

    /// <summary>
    /// 触发指定类型的事件，遍历所有匹配的订阅者并执行其动作。
    /// </summary>
    /// <param name="type">事件类型</param>
    /// <param name="ctx">事件上下文，TargetUnit 为"事件另一方"</param>
    /// <param name="instigator">触发者（触发该事件的单位），不为 null 时只触发该单位的订阅（用于 OnDealDamage/OnTakeDamage/OnKill 等）</param>
    public void Fire(EventType type, Context ctx, Unit instigator = null)
    {
        if (!_subscriptions.TryGetValue(type, out var list))
        {
            GD.Print($"[EventBus] Fire({type}) instigator={(instigator?.UnitData?.UnitName ?? "所有人")} — 无订阅者");
            return;
        }

        GD.Print($"[EventBus] >>> Fire({type}) instigator={(instigator?.UnitData?.UnitName ?? "所有人")} 订阅者数={list.Count}");

        // 事件载荷基座：克隆之源。effectCtx 从它全量继承（见下方"创建效果上下文"）
        Context baseCtx = ctx ?? new Context();

        int triggered = 0;
        // 伤害修饰初值（攻击前/受击前被动经 ModifyDamageAction 修改后回写累加）
        int dmgBase = baseCtx.DamageModifier;

        // 快照遍历，防止递归 Fire 修改原 List 导致异常
        foreach (var entry in list.ToList())
        {
            var owner = entry.Owner;

            // ── 订阅者类型分支：Unit（单位被动）/ Card（手牌被动）/ Environment（环境被动） ──
            Unit sourceUnit = null;   // 效果来源单位（Card/Environment 订阅者继承事件 ctx 的来源或施加者，可能为 null）
            bool isCardOwner = false;
            bool isEnvOwner = false;
            Environment envOwner = null;
            if (owner is Unit unit)
            {
                // 亡语：OnUnitDeath 允许死者触发自身的被动效果
                // 其他事件只触发存活单位的被动
                if (type != EventType.OnUnitDeath && (!unit.IsAlive || unit.IsDead))
                    continue;

                if (instigator != null && owner != instigator) continue;
                sourceUnit = unit;
            }
            else if (owner is Card card)
            {
                // 手牌被动：不在手牌不触发；instigator 定向不限制（由 Conditions 自行控制，如"击杀回费"）
                var cm = CardManager.Instance;
                if (cm == null || !cm.HandCards.Contains(card))
                    continue;

                // OnDrawCard 只响应"自己被抽到"（SourceCard==自己），避免被动卡连锁抽牌递归
                if (type == EventType.OnDrawCard && baseCtx.SourceCard != card)
                    continue;

                isCardOwner = true;
                sourceUnit = ctx?.SourceUnit;
            }
            else if (owner is Environment env)
            {
                // 环境被动：来源 = 环境的施加者（可能为 null）；不响应 instigator 定向（同 Card，由 Conditions 自行控制）
                isEnvOwner = true;
                envOwner = env;
                sourceUnit = env.SourceUnit;

                // 进入/离开格子事件：
                //   ① 格子匹配：仅"目标格子 == 环境所在格"的订阅者触发
                //   ② 环境变化：对面格子（Enter=旧格/召唤无，Leave=新格/死亡无）的环境
                //      与本环境 ID 相同（含对面无环境=null）→ 同一环境内移动，不触发；
                //      仅起终点环境改变（无→有 / 有→无 / 环境A→环境B）才触发。
                if (type == EventType.OnUnitEnterCell || type == EventType.OnUnitLeaveCell)
                {
                    if (baseCtx.TargetCell != env.Cell) continue;
                    var otherEnv = baseCtx.SourceCell?.Environment;
                    if (otherEnv != null && otherEnv.Data.EnvironmentID == env.Data.EnvironmentID)
                        continue;
                }
            }
            else
            {
                continue;
            }

            // 触发次数限制检查（按订阅条目独立计数）
            if (entry.MaxTriggerCount > 0)
            {
                if (_triggerCounts.TryGetValue(entry, out var tc) && tc.current <= 0)
                {
                    GD.Print($"[EventBus]   跳过: {GetOwnerName(owner)} — 已达触发上限({entry.MaxTriggerCount})");
                    continue;
                }
            }

            triggered++;

            // ── 创建效果上下文 ──────────────────────────────
            // 从事件 ctx 克隆（全量继承载荷：TargetCell/SourceCell/SourceCard/Map/ActiveUnits/
            // ActType/AttackDirection/PendingDamage/...，新增字段自动携带），只覆盖"订阅者语义"字段——
            // 替代旧版三处 new Context{} 手写白名单透传（漏传即静默丢数据，且每加字段要改 3 处联动）
            Team sourceTeam = Team.Neutral;
            if (isCardOwner)
                sourceTeam = baseCtx.SourceTeam;
            else if (owner is Unit uOwner)
                sourceTeam = uOwner.Team;
            else if (owner is Environment eOwner)
                sourceTeam = eOwner.SourceUnit?.Team ?? Team.Neutral;

            var effectCtx = baseCtx.Clone();
            effectCtx.SourceUnit = sourceUnit;
            effectCtx.SourceTeam = sourceTeam;
            effectCtx.DamageModifier = dmgBase;   // 伤害修饰统一初值（保持"两侧累加 + diff 回写"语义）
            // 派生语义：EventOtherUnit = 事件另一方（ctx.TargetUnit 透传，见 README——
            // 调用点只填 TargetUnit，不填 EventOtherUnit；克隆不会自动派生，需显式赋值）
            effectCtx.EventOtherUnit = baseCtx.TargetUnit;

            if (entry.TargetFilter != null)
            {
                // 使用 TargetFilter 自动搜索目标
                Cell centerCell = null;
                var map = MapManager.Instance?.Map;

                // 中心格子：Unit 用自身格子；Environment 用环境所在格子；Card 继承事件 ctx 的格子或来源单位格子（无则 null）
                Vector2I centerPos = default;
                bool hasCenter = false;
                if (owner is Unit uCenter)
                {
                    centerPos = uCenter.GridPos;
                    hasCenter = true;
                }
                else if (owner is Environment eCenter)
                {
                    centerPos = eCenter.Cell?.GridPos ?? default;
                    hasCenter = eCenter.Cell != null;
                }
                else if (baseCtx.TargetCell != null)
                {
                    centerPos = baseCtx.TargetCell.GridPos;
                    hasCenter = true;
                }
                else if (sourceUnit != null)
                {
                    centerPos = sourceUnit.GridPos;
                    hasCenter = true;
                }

                if (map != null && hasCenter)
                    map.TryGetValue(centerPos, out centerCell);

                // 目标解析上下文：克隆事件载荷 + 覆盖订阅者语义（中心格/环境命中单位）
                var resolveCtx = baseCtx.Clone();
                resolveCtx.SourceUnit = sourceUnit;
                resolveCtx.SourceTeam = sourceTeam;
                resolveCtx.TargetCell = centerCell;
                // 环境被动：SingleUnit 形状命中"事件单位（进入/离开）"，回退"当前格子上单位"
                resolveCtx.TargetUnit = isEnvOwner ? (baseCtx.TargetUnit ?? envOwner.Cell?.OccupyingUnit) : null;
                resolveCtx.Map = map;
                resolveCtx.ActiveUnits = UnitManager.Instance?.ActiveUnits;

                if (entry.TargetFilter.GetKind() == TargetKind.Cell)
                {
                    var cells = TargetResolver.ResolveCells(entry.TargetFilter, resolveCtx);
                    GD.Print($"[EventBus]   -> 触发: {GetOwnerName(owner)} " +
                             $"filter={entry.TargetFilter.GetType().Name} 找到格子={cells?.Length ?? 0}");
                    effectCtx.TargetCells = cells;
                }
                else
                {
                    var targets = TargetResolver.ResolveUnits(entry.TargetFilter, resolveCtx);
                    GD.Print($"[EventBus]   -> 触发: {GetOwnerName(owner)} " +
                             $"filter={entry.TargetFilter.GetType().Name} 找到目标={targets?.Length ?? 0}");
                    effectCtx.TargetUnits = targets;
                }

                // 语义保持：filter 路径动作目标由 TargetUnits/TargetCells 承载，
                // TargetUnit 置 null（基线行为），避免事件 TargetUnit 污染单目标语义
                effectCtx.TargetUnit = null;
            }
            else
            {
                // 传统 PassiveTarget 逻辑（克隆已全量继承事件载荷，仅覆盖目标角色与手牌被动特例）
                GD.Print($"[EventBus]   -> 触发: {GetOwnerName(owner)} " +
                         $"targetMode={entry.PassiveTarget} otherParty={baseCtx.TargetUnit?.UnitData?.UnitName}");

                // Card/Environment 订阅者无自身单位：Self → null；EventOther → 事件另一方
                Unit selfUnit = owner is Unit uSelf ? uSelf : null;
                effectCtx.TargetUnit = entry.PassiveTarget == PassiveTarget.Self
                    ? selfUnit : (baseCtx.TargetUnit ?? selfUnit);
                // 原语义保留：手牌被动不读 SourceCard（避免"出牌后"被动读到被出的牌自身）
                if (isCardOwner) effectCtx.SourceCard = null;
            }

            // ── 条件检查（ECA，逐个打印结果便于排查"条件不满足"） ────
            bool conditionsMet = true;
            if (entry.Conditions != null)
            {
                foreach (var cond in entry.Conditions)
                {
                    if (cond == null) continue;
                    bool met = cond.IsMet(effectCtx);
                    GD.Print($"[EventBus]     条件[{DescribeCondition(cond, effectCtx)}] = {met}");
                    if (!met) conditionsMet = false;
                }
            }

            if (!conditionsMet)
            {
                GD.Print($"[EventBus]   条件不满足，跳过: {GetOwnerName(owner)}");
                continue;
            }

            // 扣减触发次数（执行前扣减：动作可能触发嵌套 Fire（如"抽牌再抽牌"），
            // 若执行后再扣，嵌套 Fire 中计数未扣会重复触发，导致无限递归栈溢出）
            if (entry.MaxTriggerCount > 0)
            {
                if (_triggerCounts.TryGetValue(entry, out var tc))
                    _triggerCounts[entry] = (tc.max, tc.current - 1);
            }

            foreach (var action in entry.Actions)
                action?.Execute(effectCtx);

            // 伤害修饰增量回写（多个加伤/减伤被动叠加到调用方 ctx）
            if (ctx != null && effectCtx.DamageModifier != dmgBase)
                ctx.DamageModifier += effectCtx.DamageModifier - dmgBase;
        }

        GD.Print($"[EventBus] <<< Fire({type}) 完成，实际触发 {triggered} 个");
    }

    /// <summary>
    /// 每回合开始时调用，重置所有被动效果的触发计数
    /// </summary>
    public void ResetTriggerCounts()
    {
        var keys = _triggerCounts.Keys.ToList();
        foreach (var key in keys)
        {
            var tc = _triggerCounts[key];
            _triggerCounts[key] = (tc.max, tc.max);
        }
        GD.Print($"[EventBus] 重置 {keys.Count} 个触发计数");
    }

    /// <summary>订阅者显示名（Unit → UnitName；Card → CardID）</summary>
    private static string GetOwnerName(object owner)
    {
        if (owner is Unit u) return u.UnitData?.UnitName ?? $"Unit#{u.ID}";
        if (owner is Card c) return c.CardID;
        return "?";
    }

    /// <summary>条件诊断描述（类型 + 关键数值/检查目标），便于排查"条件不满足"</summary>
    private static string DescribeCondition(Condition cond, Context ctx)
    {
        if (cond is CompareCondition cmp)
        {
            int l = cmp.Left?.GetValue(ctx) ?? 0;
            int r = cmp.Right?.GetValue(ctx) ?? 0;
            return $"{cond.GetType().Name}: {l} {cmp.Op} {r}";
        }
        if (cond is HasBuffCondition hb)
        {
            var u = hb.CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
            return $"{cond.GetType().Name}: {hb.CheckTarget}={u?.UnitData?.UnitName ?? "null"} Buff={hb.BuffID} Has={hb.Has}";
        }
        if (cond is HasTagCondition ht)
        {
            var u = ht.CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
            return $"{cond.GetType().Name}: {ht.CheckTarget}={u?.UnitData?.UnitName ?? "null"} Tags=[{string.Join(",", ht.Tags)}] Has={ht.Has}";
        }
        if (cond is HasActedCondition ha)
        {
            var u = ha.CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
            return $"{cond.GetType().Name}: {ha.CheckTarget}={u?.UnitData?.UnitName ?? "null"} 已行动={u?.ActionsThisTurn ?? 0} 要求={ha.HasActed}";
        }
        return cond.GetType().Name;
    }
}
