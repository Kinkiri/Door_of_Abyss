using Godot;

/// <summary>
/// "本回合已行动过"条件：判断来源或目标单位本回合的行动次数（移动/攻击各算一次）。
/// 行动计数由 BattleManager 在行动结算时 +1，RoundStart 归零（Unit.ActionsThisTurn）。
/// 不依赖 AP 比较——AP 可被"透支"类效果超过上限，行动次数不受影响。
/// </summary>
[GlobalClass]
public partial class HasActedCondition : Condition
{
    [Export(PropertyHint.Enum, "来源,目标")] public ConditionTarget CheckTarget { get; set; } = ConditionTarget.Source;

    /// <summary>true=本回合已行动过，false=本回合尚未行动</summary>
    [Export] public bool HasActed { get; set; } = true;

    public override bool IsMet(Context ctx)
    {
        var unit = CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null) return false;

        bool acted = unit.ActionsThisTurn > 0;
        return acted == HasActed;
    }
}
