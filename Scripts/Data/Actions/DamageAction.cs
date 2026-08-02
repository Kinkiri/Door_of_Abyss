using Godot;

/// <summary>
/// 造成伤害。可复用于卡牌和被动效果。
/// </summary>
[GlobalClass]
public partial class DamageAction : GameAction
{
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnits == null) return;

        int dmg = ValueSource?.GetValue(ctx) ?? Value;

        foreach (var target in ctx.TargetUnits)
        {
            if (target == null || !target.IsAlive) continue;

            int finalDmg = dmg;
            if (ctx.SourceUnit != null)
            {
                // 伤害计算前触发两个独立事件（各视角 Source/Target 语义完整）：
                //   OnBeforeAttack      攻击者视角：Source=攻击者，Target=受击者 → 加伤被动（读 Source=自己）
                //   OnBeforeTakeDamage  受击者视角：Source=受击者，Target=攻击者 → 减伤被动（读 Source=自己）
                // 被动用 ModifyDamageAction 修改各自 ctx.DamageModifier，两侧增量累加；
                // PendingDamage 暴露本次基础伤害，供被动判断"伤害是否会致死"
                var attackCtx = new Context { SourceUnit = ctx.SourceUnit, TargetUnit = target, PendingDamage = dmg };
                var defendCtx = new Context { SourceUnit = target, TargetUnit = ctx.SourceUnit, PendingDamage = dmg };
                EventBus.Instance?.Fire(EventType.OnBeforeAttack, attackCtx, subject: ctx.SourceUnit);
                EventBus.Instance?.Fire(EventType.OnBeforeTakeDamage, defendCtx, subject: target);
                finalDmg = System.Math.Max(0, dmg + attackCtx.DamageModifier + defendCtx.DamageModifier);
            }

            int dealt = UnitManager.Instance.DamageUnit(target, finalDmg);
            if (dealt <= 0) continue;

            GD.Print($"[DamageAction] 对 {target.UnitData?.UnitName} 造成 {dealt} 点伤害");

            // 有来源单位时触发战斗被动事件
            if (ctx.SourceUnit != null)
            {
                EventBus.Instance?.Fire(EventType.OnDealDamage,
                    new Context { TargetUnit = target }, subject: ctx.SourceUnit);
                EventBus.Instance?.Fire(EventType.OnTakeDamage,
                    new Context { TargetUnit = ctx.SourceUnit }, subject: target);
                if (!target.IsAlive)
                    EventBus.Instance?.Fire(EventType.OnKill,
                        new Context { TargetUnit = target }, subject: ctx.SourceUnit);
            }
        }
    }
}
