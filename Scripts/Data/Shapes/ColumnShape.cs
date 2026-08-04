using Godot;
using System.Collections.Generic;

/// <summary>
/// 整列形状：中心所在列的上下各 Length 格（含中心，共 2×Length+1 格）。
/// </summary>
[GlobalClass]
public partial class ColumnShape : CellShape
{
    [Export] public int Length { get; set; } = 1;

    /// <summary>动态臂长值源，配置后覆盖 Length</summary>
    [Export] public ValueSource LengthValueSource { get; set; }

    public override Cell[] GetCells(Cell center, Context ctx) => GetCells(center, ctx, -1);

    public override Cell[] GetCells(Cell center, Context ctx, int sizeOverride)
    {
        if (center == null || ctx.Map == null) return System.Array.Empty<Cell>();
        int len = sizeOverride >= 0 ? sizeOverride : (LengthValueSource?.GetValue(ctx) ?? Length);
        if (len < 0) len = 0;

        var list = new List<Cell>(len * 2 + 1);
        for (int dy = -len; dy <= len; dy++)
        {
            var pos = new Vector2I(center.GridPos.X, center.GridPos.Y + dy);
            if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                list.Add(c);
        }
        return list.ToArray();
    }

    public override TargetShape GetCategory() => TargetShape.Column;

    public override int GetAreaRange() => Length;
}
