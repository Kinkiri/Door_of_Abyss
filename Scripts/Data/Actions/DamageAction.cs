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
                // 伤害计算前触发：攻击者侧（加伤被动）+ 受击者侧（减伤被动）各一次，
                // 被动用 ModifyDamageAction 修改 beforeCtx.DamageModifier
                var beforeCtx = new Context { TargetUnit = target };
                EventBus.Instance?.Fire(EventType.OnBeforeDamage, beforeCtx, subject: ctx.SourceUnit);
                EventBus.Instance?.Fire(EventType.OnBeforeDamage, beforeCtx, subject: target);
                finalDmg = System.Math.Max(0, dmg + beforeCtx.DamageModifier);
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
