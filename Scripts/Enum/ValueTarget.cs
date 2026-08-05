/// <summary>取值目标</summary>
public enum ValueTarget
{
    Source,
    Target,

    /// <summary>事件另一方单位（由 EventBus 构建 effectCtx 时经 Context.EventOtherUnit 提供，死亡事件=死者）</summary>
    EventOther,
}
