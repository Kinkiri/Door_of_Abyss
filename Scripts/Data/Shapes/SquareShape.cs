using Godot;

/// <summary>
/// 方形扩散形状：以中心格为中心、dx/dy 各 ±AreaRange 的格子（含中心）。
/// </summary>
[GlobalClass]
public partial class SquareShape : CellShape
{
    [Export] public int AreaRange { get; set; } = 1;

    /// <summary>动态扩散半径值源，配置后覆盖 AreaRange</summary>
    [Export] public ValueSource AreaRangeValueSource { get; set; }

    public override Cell[] GetCells(Cell center, Context ctx) => GetCells(center, ctx, -1);

    public override Cell[] GetCells(Cell center, Context ctx, int sizeOverride)
    {
        int range = sizeOverride >= 0 ? sizeOverride : (AreaRangeValueSource?.GetValue(ctx) ?? AreaRange);
        return TargetResolver.CellsInArea(center, range, diamond: false, ctx.Map);
    }

    public override TargetShape GetCategory() => TargetShape.AreaSquare;

    public override int GetAreaRange() => AreaRange;
}
