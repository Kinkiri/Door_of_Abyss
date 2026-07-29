using Godot;

/// <summary>
/// 与条件：所有子条件通过才算通过。
/// 子条件为空时视为通过（方便临时禁用某个分支）。
/// </summary>
[GlobalClass]
public partial class AndCondition : Condition
{
    [Export] public Condition[] Conditions { get; set; }

    public override bool IsMet(Context ctx)
    {
        if (Conditions == null) return true;

        foreach (var c in Conditions)
        {
            if (c != null && !c.IsMet(ctx))
                return false;
        }
        return true;
    }
}
