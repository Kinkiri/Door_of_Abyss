using Godot;

/// <summary>
/// 被动效果数据，定义单位在特定事件触发时执行的动作序列
/// </summary>
[GlobalClass]
public partial class EffectData : Resource
{
    /// <summary>触发事件类型</summary>
    [Export] public EventType TriggerEvent { get; set; }

    /// <summary>
    /// 目标选择方式（Shape==None 时生效）
    /// </summary>
    [Export] public PassiveTarget Target { get; set; } = PassiveTarget.Self;

    /// <summary>
    /// 目标筛选器数组（默认 And 组合）。为 null/空时使用 Target（Self/EventTarget）选择目标。
    /// 替代旧 Shape + Filter + AreaRange 三个字段。
    /// </summary>
    [Export] public TargetFilter[] TargetFilters { get; set; }

    /// <summary>每回合最大触发次数，0=不限制</summary>
    [Export] public int MaxTriggerCount { get; set; } = 0;

    /// <summary>触发时要执行的动作序列</summary>
    [Export] public GameAction[] Actions { get; set; }

    /// <summary>
    /// 执行条件。不满足条件的触发不会执行 Actions。
    /// 多个条件之间是 AND 关系。为 null 或空时无条件限制。
    /// </summary>
    [Export] public Condition[] Conditions { get; set; }
}
