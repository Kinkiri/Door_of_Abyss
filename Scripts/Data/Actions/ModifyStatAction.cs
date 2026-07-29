using Godot;

/// <summary>
/// 修改单位战斗属性（可逆，Buff 到期自动还原）。
/// 修改固定数值而非百分比，叠层时效果线性叠加。
/// </summary>
[GlobalClass]
public partial class ModifyStatAction : GameAction
{
    [Export] public ModifyStatType TargetStat;
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null) return;

        int val = ValueSource?.GetValue(ctx) ?? Value;

        switch (TargetStat)
        {
            case ModifyStatType.AttackPower:
                ctx.TargetUnit.AttackPower += val;
                break;
            case ModifyStatType.MaxHP:
                ctx.TargetUnit.MaxHP += val;
                // "只加不减"：CurrentHP 不随上限升高
                break;
            case ModifyStatType.Stamina:
                ctx.TargetUnit.MaxStamina += val;
                break;
            case ModifyStatType.AttackDistance:
                ctx.TargetUnit.AttackDistance += val;
                break;
            case ModifyStatType.ActionPoints:
                ctx.TargetUnit.ActionPoints += val;
                break;
        }

        ctx.TargetUnit.UpdateUnit();
        GD.Print($"[ModifyStatAction] {TargetStat} {val:+0;-0} → {ctx.TargetUnit.UnitData?.UnitName}");
    }

    public override void Revert(Context ctx)
    {
        if (ctx?.TargetUnit == null) return;

        int val = ValueSource?.GetValue(ctx) ?? Value;

        switch (TargetStat)
        {
            case ModifyStatType.AttackPower:
                ctx.TargetUnit.AttackPower -= val;
                break;
            case ModifyStatType.MaxHP:
                ctx.TargetUnit.MaxHP -= val;
                // 还原时若 CurrentHP 超出新上限则截断
                ctx.TargetUnit.CurrentHP = Mathf.Min(ctx.TargetUnit.CurrentHP, ctx.TargetUnit.MaxHP);
                break;
            case ModifyStatType.Stamina:
                ctx.TargetUnit.MaxStamina -= val;
                break;
            case ModifyStatType.AttackDistance:
                ctx.TargetUnit.AttackDistance -= val;
                break;
            case ModifyStatType.ActionPoints:
                ctx.TargetUnit.ActionPoints -= val;
                break;
        }

        ctx.TargetUnit.UpdateUnit();
        GD.Print($"[ModifyStatAction] 还原 {TargetStat} {val:+0;-0} → {ctx.TargetUnit.UnitData?.UnitName}");
    }
}
