using Godot;
using System.Collections.Generic;

/// <summary>
/// 整行形状：中心所在行的左右各 Length 格（含中心，共 2×Length+1 格）。
/// </summary>
[GlobalClass]
public partial class RowShape : CellShape
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
        for (int dx = -len; dx <= len; dx++)
        {
            var pos = new Vector2I(center.GridPos.X + dx, center.GridPos.Y);
            if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                list.Add(c);
        }
        return list.ToArray();
    }

    public override TargetShape GetCategory() => TargetShape.Row;

    public override int GetAreaRange() => Length;
}
