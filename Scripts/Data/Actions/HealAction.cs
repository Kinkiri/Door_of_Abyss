using Godot;

/// <summary>
/// 治疗。可复用于卡牌和被动效果。
/// </summary>
[GlobalClass]
public partial class HealAction : GameAction
{
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnits == null) return;

        int heal = ValueSource?.GetValue(ctx) ?? Value;

        foreach (var target in ctx.TargetUnits)
        {
            if (target == null || !target.IsAlive) continue;
            UnitManager.Instance.HealUnit(target, heal);
            GD.Print($"[HealAction] 治疗 {target.UnitData?.UnitName} {heal} 点");
        }
    }
}
