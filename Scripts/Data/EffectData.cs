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
    /// 范围形状。不为 None 时使用 TargetResolver 自动搜索目标，忽略 Target 字段。
    /// </summary>
    [Export] public TargetShape Shape { get; set; } = TargetShape.None;

    /// <summary>目标阵营过滤（Shape!=None 时生效）</summary>
    [Export] public TargetFilter Filter { get; set; } = TargetFilter.All;

    /// <summary>范围扩散半径（Shape 为 AreaDiamond/AreaSquare 时）</summary>
    [Export] public int AreaRange { get; set; } = 1;

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
