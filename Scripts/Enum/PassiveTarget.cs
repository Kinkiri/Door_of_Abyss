/// <summary>被动效果的目标选择方式</summary>
public enum PassiveTarget
{
    Self,

    /// <summary>事件另一方（操作目标 = 触发被动的事件的对方，死亡事件=死者）</summary>
    EventOther,
}
