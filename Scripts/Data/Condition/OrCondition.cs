using Godot;

/// <summary>
/// 或条件：任一子条件通过就算通过。
/// 子条件为空时视为不通过。
/// </summary>
[GlobalClass]
public partial class OrCondition : Condition
{
    [Export] public Condition[] Conditions { get; set; }

    public override bool IsMet(Context ctx)
    {
        if (Conditions == null) return false;

        foreach (var c in Conditions)
        {
            if (c != null && c.IsMet(ctx))
                return true;
        }
        return false;
    }
}
