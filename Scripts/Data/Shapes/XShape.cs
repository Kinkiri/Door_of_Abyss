using Godot;
using System.Collections.Generic;

/// <summary>
/// 叉字形状：中心 + 四对角方向各 Length 格（含中心，共 4×Length+1 格）。
/// </summary>
[GlobalClass]
public partial class XShape : CellShape
{
    [Export] public int Length { get; set; } = 1;

    /// <summary>动态臂长值源，配置后覆盖 Length</summary>
    [Export] public ValueSource LengthValueSource { get; set; }

    private static readonly Vector2I[] DiagDirs =
    {
        new Vector2I(1, -1),  // ↗
        new Vector2I(-1, -1), // ↖
        new Vector2I(1, 1),   // ↘
        new Vector2I(-1, 1),  // ↙
    };

    public override Cell[] GetCells(Cell center, Context ctx) => GetCells(center, ctx, -1);

    public override Cell[] GetCells(Cell center, Context ctx, int sizeOverride)
    {
        if (center == null || ctx.Map == null) return System.Array.Empty<Cell>();
        int len = sizeOverride >= 0 ? sizeOverride : (LengthValueSource?.GetValue(ctx) ?? Length);
        if (len < 0) len = 0;

        var list = new List<Cell>((len + 1) * 4 + 1);
        if (ctx.Map.TryGetValue(center.GridPos, out Cell c0) && c0 != null)
            list.Add(c0);
        for (int i = 1; i <= len; i++)
            foreach (var d in DiagDirs)
            {
                var pos = center.GridPos + d * i;
                if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                    list.Add(c);
            }
        return list.ToArray();
    }

    public override TargetShape GetCategory() => TargetShape.X;

    public override int GetAreaRange() => Length;
}
