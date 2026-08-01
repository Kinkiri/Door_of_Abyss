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

    /// <summary>触发计数追踪：(owner, type) → (maxCount, currentCount)</summary>
    private Dictionary<(Unit owner, EventType type), (int max, int current)> _triggerCounts = new();

    private struct Subscription
    {
        public Unit Owner;
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
    /// 为单位注册被动效果（无标签，用于单位原生被动）
    /// </summary>
    public void Subscribe(Unit owner, EffectData[] effects)
    {
        Subscribe(owner, effects, null);
    }

    /// <summary>
    /// 为单位注册被动效果（带标签，用于 Buff 等临时效果）
    /// </summary>
    /// <param name="tag">订阅标签，Buff 到期时通过此标签单独清理</param>
    public void Subscribe(Unit owner, EffectData[] effects, string tag)
    {
        if (effects == null) return;

        GD.Print($"[EventBus] === 订阅: {owner.UnitData?.UnitName} ID={owner.ID} tag={tag ?? "null"} ===");

        foreach (var effect in effects)
        {
            if (effect?.Actions == null) continue;

            if (!_subscriptions.TryGetValue(effect.TriggerEvent, out var list))
            {
                list = new List<Subscription>();
                _subscriptions[effect.TriggerEvent] = list;
            }

            list.Add(new Subscription
            {
                Owner = owner,
                Actions = effect.Actions,
                PassiveTarget = effect.Target,
                TargetFilter = TargetFilter.CombineAnd(effect.TargetFilters),
                MaxTriggerCount = effect.MaxTriggerCount,
                Conditions = effect.Conditions,
                Tag = tag,
            });

            // 初始化触发计数
            if (effect.MaxTriggerCount > 0)
            {
                var key = (owner, effect.TriggerEvent);
                _triggerCounts[key] = (effect.MaxTriggerCount, effect.MaxTriggerCount);
            }

            GD.Print($"[EventBus]   注册 {effect.TriggerEvent} target={effect.Target} " +
                     $"targetFilter={TargetFilter.CombineAnd(effect.TargetFilters)?.GetType().Name ?? "null"} maxTrigger={effect.MaxTriggerCount} " +
                     $"action={string.Join(",", Array.ConvertAll(effect.Actions, a => a?.GetType().Name ?? "null"))}");
        }
    }

    /// <summary>
    /// 移除单位的所有被动效果订阅（单位销毁时调用）
    /// </summary>
    public void Unsubscribe(Unit owner)
    {
        GD.Print($"[EventBus] === 取消订阅: {owner.UnitData?.UnitName} ID={owner.ID} ===");
        int removed = 0;
        foreach (var kv in _subscriptions)
        {
            removed += kv.Value.RemoveAll(s => s.Owner == owner);
        }
        // 清理触发计数
        var keys = _triggerCounts.Keys.Where(k => k.owner == owner).ToList();
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
    /// <param name="subject">触发者，不为 null 时只触发该单位的订阅（用于 OnDealDamage/OnTakeDamage/OnKill 等）</param>
    public void Fire(EventType type, Context ctx, Unit subject = null)
    {
        if (!_subscriptions.TryGetValue(type, out var list))
        {
            GD.Print($"[EventBus] Fire({type}) subject={(subject?.UnitData?.UnitName ?? "所有人")} — 无订阅者");
            return;
        }

        GD.Print($"[EventBus] >>> Fire({type}) subject={(subject?.UnitData?.UnitName ?? "所有人")} 订阅者数={list.Count}");

        int triggered = 0;
        // 快照遍历，防止递归 Fire 修改原 List 导致异常
        foreach (var entry in list.ToList())
        {
            var owner = entry.Owner;

            // 亡语：OnUnitDeath 允许死者触发自身的被动效果
            // 其他事件只触发存活单位的被动
            if (type != EventType.OnUnitDeath && (!owner.IsAlive || owner.IsDead))
                continue;

            if (subject != null && owner != subject) continue;

            // 触发次数限制检查
            if (entry.MaxTriggerCount > 0)
            {
                var key = (owner, type);
                if (_triggerCounts.TryGetValue(key, out var tc) && tc.current <= 0)
                {
                    GD.Print($"[EventBus]   跳过: {owner.UnitData?.UnitName} — 已达触发上限({entry.MaxTriggerCount})");
                    continue;
                }
            }

            triggered++;

            // ── 创建效果上下文 ──────────────────────────────
            Context effectCtx;

            if (entry.TargetFilter != null)
            {
                // 使用 TargetFilter 自动搜索目标
                Cell centerCell = null;
                var map = MapManager.Instance?.Map;
                if (map != null)
                    map.TryGetValue(owner.GridPos, out centerCell);

                var resolveCtx = new Context
                {
                    SourceUnit = owner,
                    TargetCell = centerCell,
                    SourceTeam = owner.Team,
                    Map = map,
                    ActiveUnits = UnitManager.Instance?.ActiveUnits,
                };

                if (entry.TargetFilter.GetKind() == TargetKind.Cell)
                {
                    var cells = TargetResolver.ResolveCells(entry.TargetFilter, resolveCtx);
                    GD.Print($"[EventBus]   -> 触发: {owner.UnitData?.UnitName} ID={owner.ID} " +
                             $"filter={entry.TargetFilter.GetType().Name} 找到格子={cells?.Length ?? 0}");
                    effectCtx = new Context
                    {
                        SourceUnit = owner,
                        TargetCells = cells,
                        SourceTeam = owner.Team,
                        TargetTeam = Team.Neutral,
                    };
                }
                else
                {
                    var targets = TargetResolver.ResolveUnits(entry.TargetFilter, resolveCtx);
                    GD.Print($"[EventBus]   -> 触发: {owner.UnitData?.UnitName} ID={owner.ID} " +
                             $"filter={entry.TargetFilter.GetType().Name} 找到目标={targets?.Length ?? 0}");
                    effectCtx = new Context
                    {
                        SourceUnit = owner,
                        TargetUnits = targets,
                        SourceTeam = owner.Team,
                        TargetTeam = Team.Neutral,
                    };
                }
            }
            else
            {
                // 传统 PassiveTarget 逻辑
                GD.Print($"[EventBus]   -> 触发: {owner.UnitData?.UnitName} ID={owner.ID} " +
                         $"targetMode={entry.PassiveTarget} otherParty={ctx?.TargetUnit?.UnitData?.UnitName}");

                effectCtx = new Context
                {
                    SourceUnit = owner,
                    TargetUnit = entry.PassiveTarget == PassiveTarget.Self
                        ? owner : (ctx?.TargetUnit ?? owner),
                    TargetCell = ctx?.TargetCell,
                    SourceCell = ctx?.SourceCell,
                    SourceCard = ctx?.SourceCard,
                    SourceTeam = ctx?.SourceTeam ?? Team.Neutral,
                    TargetTeam = ctx?.TargetTeam ?? Team.Neutral,
                };
            }

            // ── 条件检查（ECA） ──────────────────────────────
            bool conditionsMet = true;
            if (entry.Conditions != null)
            {
                foreach (var cond in entry.Conditions)
                {
                    if (cond != null && !cond.IsMet(effectCtx))
                    {
                        conditionsMet = false;
                        break;
                    }
                }
            }

            if (!conditionsMet)
            {
                GD.Print($"[EventBus]   条件不满足，跳过: {owner.UnitData?.UnitName}");
                continue;
            }

            foreach (var action in entry.Actions)
                action?.Execute(effectCtx);

            // 扣减触发次数
            if (entry.MaxTriggerCount > 0)
            {
                var key = (owner, type);
                if (_triggerCounts.TryGetValue(key, out var tc))
                    _triggerCounts[key] = (tc.max, tc.current - 1);
            }
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
}
