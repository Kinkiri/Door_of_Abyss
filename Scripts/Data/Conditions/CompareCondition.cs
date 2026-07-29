using Godot;

/// <summary>
/// 通用比较条件：比较两个值源的结果。
/// 替换 HPCondition 和 CompareStatCondition，支持任意数值比较。
/// </summary>
[GlobalClass]
public partial class CompareCondition : Condition
{
    [Export] public ValueSource Left { get; set; }

    [Export] public CompareOp Op { get; set; } = CompareOp.GreaterEqual;

    [Export] public ValueSource Right { get; set; }

    public override bool IsMet(Context ctx)
    {
        int l = Left?.GetValue(ctx) ?? 0;
        int r = Right?.GetValue(ctx) ?? 0;

        return Op switch
        {
            CompareOp.Less => l < r,
            CompareOp.LessEqual => l <= r,
            CompareOp.Greater => l > r,
            CompareOp.GreaterEqual => l >= r,
            CompareOp.Equal => l == r,
            CompareOp.NotEqual => l != r,
            _ => true,
        };
    }
}
