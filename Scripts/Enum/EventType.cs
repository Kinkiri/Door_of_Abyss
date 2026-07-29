/// <summary>
/// 触发被动效果的事件类型
/// </summary>
public enum EventType
{
    /// <summary>每回合开始</summary>
    RoundStart,

    /// <summary>每回合结束</summary>
    RoundEnd,

    /// <summary>单位登场</summary>
    OnSpawn,

    /// <summary>造成伤害后（SourceUnit = 攻击者，TargetUnit = 被攻击者）</summary>
    OnDealDamage,

    /// <summary>受到伤害后（SourceUnit = 受击者，TargetUnit = 攻击者）</summary>
    OnTakeDamage,

    /// <summary>击杀后（SourceUnit = 击杀者，TargetUnit = 死者）</summary>
    OnKill,

    /// <summary>Buff 施加后</summary>
    OnBuffApplied,

    /// <summary>Buff 移除后</summary>
    OnBuffRemoved,

    /// <summary>单位行动后（移动/攻击/出牌），subject=行动单位</summary>
    OnUnitAct,
}
