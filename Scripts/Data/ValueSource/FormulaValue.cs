using Godot;

/// <summary>
/// 公式值源，对两个子值源进行二元运算，支持任意嵌套。
/// </summary>
[GlobalClass]
public partial class FormulaValue : ValueSource
{
    [Export] public FormulaOp Op { get; set; } = FormulaOp.Add;

    [Export] public ValueSource Left { get; set; }

    [Export] public ValueSource Right { get; set; }

    public override int GetValue(Context ctx)
    {
        int l = Left?.GetValue(ctx) ?? 0;
        int r = Right?.GetValue(ctx) ?? 0;

        return Op switch
        {
            FormulaOp.Add => l + r,
            FormulaOp.Sub => l - r,
            FormulaOp.Mul => l * r,
            FormulaOp.Div => r == 0 ? 0 : l / r,
            FormulaOp.Max => System.Math.Max(l, r),
            FormulaOp.Min => System.Math.Min(l, r),
            FormulaOp.Percent => l * r / 100,
            _ => 0,
        };
    }
}
