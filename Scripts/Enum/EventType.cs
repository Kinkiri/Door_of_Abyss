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

    /// <summary>
    /// 伤害计算前触发（攻击者侧+受击者侧各一次）。已废弃：不再触发，
    /// 拆分为 OnBeforeAttack（攻击者侧）与 OnBeforeTakeDamage（受击者侧）。
    /// 保留枚举值防止已有 .tres 数值错位。
    /// </summary>
    OnBeforeDamage,

    /// <summary>单位死亡后（SourceUnit=死者，TargetUnit=击杀者），subject=死者</summary>
    OnUnitDeath,

    /// <summary>移动后，subject=移动单位</summary>
    OnMove,

    /// <summary>使用卡牌后（出牌成功、扣费后，卡牌动作执行前），无subject</summary>
    OnUseCard,

    /// <summary>抽牌后（SourceCard=被抽的牌，SourceTeam=抽牌方），无subject；手牌被动与单位被动均可响应</summary>
    OnDrawCard,

    /// <summary>
    /// 攻击前（伤害计算前，攻击者视角，subject=攻击者）。SourceUnit=攻击者，TargetUnit=受击者，
    /// ctx.PendingDamage=本次基础伤害。攻击者挂"加伤"被动（读 Source=自己）。
    /// </summary>
    OnBeforeAttack,

    /// <summary>
    /// 受击前（伤害计算前，受击者视角，subject=受击者）。SourceUnit=受击者（自己），TargetUnit=攻击者，
    /// ctx.PendingDamage=本次基础伤害。受击者挂"减伤"被动（读 Source=自己）。
    /// </summary>
    OnBeforeTakeDamage,
}
