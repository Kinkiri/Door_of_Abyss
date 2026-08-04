using Godot;
using System.Collections.Generic;

/// <summary>
/// 环形形状：曼哈顿距离恰为 Radius 的格子（菱形环，不含内部与中心）。
/// Radius=0 → 只有中心格；Radius=N → |dx|+|dy|==N 的格子（N=1 → 上下左右 4 格）。
/// </summary>
[GlobalClass]
public partial class RingShape : CellShape
{
    [Export] public int Radius { get; set; } = 1;

    /// <summary>动态半径值源，配置后覆盖 Radius</summary>
    [Export] public ValueSource RadiusValueSource { get; set; }

    public override Cell[] GetCells(Cell center, Context ctx) => GetCells(center, ctx, -1);

    public override Cell[] GetCells(Cell center, Context ctx, int sizeOverride)
    {
        if (center == null || ctx.Map == null) return System.Array.Empty<Cell>();
        int r = sizeOverride >= 0 ? sizeOverride : (RadiusValueSource?.GetValue(ctx) ?? Radius);
        if (r < 0) r = 0;

        if (r == 0)
        {
            return ctx.Map.TryGetValue(center.GridPos, out Cell c0) && c0 != null
                ? new[] { c0 }
                : System.Array.Empty<Cell>();
        }

        var list = new List<Cell>(r * 4);
        for (int dx = -r; dx <= r; dx++)
        {
            int dyAbs = r - System.Math.Abs(dx);
            if (dyAbs == 0)
            {
                // 轴向端点（dx=±r, dy=0）：只加一次
                var pos = new Vector2I(center.GridPos.X + dx, center.GridPos.Y);
                if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                    list.Add(c);
                continue;
            }
            foreach (int dy in new[] { dyAbs, -dyAbs })
            {
                var pos = new Vector2I(center.GridPos.X + dx, center.GridPos.Y + dy);
                if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                    list.Add(c);
            }
        }
        return list.ToArray();
    }

    public override TargetShape GetCategory() => TargetShape.Ring;

    public override int GetAreaRange() => Radius;
}
