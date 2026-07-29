using Godot;

/// <summary>
/// Buff 条件：检查来源或目标是否拥有指定 ID 的 Buff。
/// </summary>
[GlobalClass]
public partial class HasBuffCondition : Condition
{
    [Export(PropertyHint.Enum, "来源,目标")] public ConditionTarget CheckTarget { get; set; } = ConditionTarget.Target;

    /// <summary>要检查的 BuffID</summary>
    [Export] public string BuffID { get; set; } = "";

    /// <summary>true=必须拥有，false=必须没有</summary>
    [Export] public bool Has { get; set; } = true;

    public override bool IsMet(Context ctx)
    {
        var unit = CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null) return false;

        bool hasBuff = BuffManager.Instance?.HasBuff(unit, BuffID) == true;
        return hasBuff == Has;
    }
}
