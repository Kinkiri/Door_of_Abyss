using Godot;

/// <summary>
/// 设置单位为精确属性值（覆盖而非增减）。
/// 不可逆——不支持 Buff 到期自动还原（因其采用绝对值覆盖，多实例共享 Resource 无法正确还原）。
/// </summary>
[GlobalClass]
public partial class SetStatAction : GameAction
{
    [Export] public ModifyStatType TargetStat { get; set; } = ModifyStatType.AttackPower;

    /// <summary>目标值（值源，覆盖静态 Value）</summary>
    [Export] public ValueSource ValueSource { get; set; }

    [Export] public int Value { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null) return;

        int val = ValueSource?.GetValue(ctx) ?? Value;

        switch (TargetStat)
        {
            case ModifyStatType.AttackPower:
                ctx.TargetUnit.AttackPower = val;
                break;
            case ModifyStatType.MaxHP:
                ctx.TargetUnit.MaxHP = val;
                break;
            case ModifyStatType.Stamina:
                ctx.TargetUnit.Stamina = val;
                break;
            case ModifyStatType.AttackDistance:
                ctx.TargetUnit.AttackDistance = val;
                break;
            case ModifyStatType.ActionPoints:
                // 设置行动点上限（最小不低于 1），当前行动点不随改（回合开始恢复满）
                ctx.TargetUnit.MaxActionPoints = System.Math.Max(1, val);
                break;
        }

        ctx.TargetUnit.UpdateUnit();
        GD.Print($"[SetStatAction] {TargetStat} → {val}");
    }
}
