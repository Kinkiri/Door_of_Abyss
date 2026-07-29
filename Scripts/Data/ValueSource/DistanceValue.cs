using Godot;

/// <summary>
/// 单位和目标之间的曼哈顿距离。
/// </summary>
[GlobalClass]
public partial class DistanceValue : ValueSource
{
    [Export] public ValueTarget From { get; set; } = ValueTarget.Source;
    [Export] public ValueTarget To { get; set; } = ValueTarget.Target;

    public override int GetValue(Context ctx)
    {
        var fromUnit = From == ValueTarget.Source ? ctx.SourceUnit : ctx.TargetUnit;
        var toUnit = To == ValueTarget.Source ? ctx.SourceUnit : ctx.TargetUnit;
        if (fromUnit == null || toUnit == null) return 0;

        return System.Math.Abs(fromUnit.GridPos.X - toUnit.GridPos.X) +
               System.Math.Abs(fromUnit.GridPos.Y - toUnit.GridPos.Y);
    }
}
