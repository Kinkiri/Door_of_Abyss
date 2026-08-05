using Godot;

/// <summary>
/// 方向值源：计算从 From 指向 To 的曼哈顿方向（4 向），返回 CellDirection 枚举值。
/// 约定与 MoveUnitAction 一致：|dx| ≥ |dy| 取横向（dx>0→Right，dx<0→Left），否则取纵向（dy>0→Down，dy<0→Up）；
/// 零向量（同格）按横向处理返回 Right。单位缺失返回 Up（配合 Step 等使用时基准无效自然跳过）。
/// </summary>
[GlobalClass]
public partial class DirectionValue : ValueSource
{
    [Export] public ValueTarget From { get; set; } = ValueTarget.Source;
    [Export] public ValueTarget To { get; set; } = ValueTarget.Target;

    public override int GetValue(Context ctx)
    {
        var fromUnit = PickUnit(From, ctx);
        var toUnit = PickUnit(To, ctx);
        if (fromUnit == null || toUnit == null) return (int)CellDirection.Up;

        int dx = toUnit.GridPos.X - fromUnit.GridPos.X;
        int dy = toUnit.GridPos.Y - fromUnit.GridPos.Y;

        if (System.Math.Abs(dx) >= System.Math.Abs(dy))
            return dx >= 0 ? (int)CellDirection.Right : (int)CellDirection.Left;
        return dy >= 0 ? (int)CellDirection.Down : (int)CellDirection.Up;
    }

    private static Unit PickUnit(ValueTarget t, Context ctx) => t switch
    {
        ValueTarget.Source => ctx.SourceUnit,
        ValueTarget.Target => ctx.TargetUnit,
        ValueTarget.EventOther => ctx.EventOtherUnit,
        _ => null,
    };
}
