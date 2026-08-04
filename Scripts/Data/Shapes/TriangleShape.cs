using Godot;
using System.Collections.Generic;

/// <summary>
/// 三角形（锥形）形状：沿 Direction 方向，每前进 1 排宽度 +2（两侧各 +1）。
/// 第 i 排（i=0..Length）宽 2i+1（1→3→5…对称锥形），含中心排，共 (Length+1)² 格。
/// </summary>
[GlobalClass]
public partial class TriangleShape : CellShape
{
    [Export] public CellDirection Direction { get; set; } = CellDirection.Up;

    /// <summary>动态方向值源（如 DirectionValue 计算朝向目标），配置后覆盖 Direction；非法值按 Up 处理</summary>
    [Export] public ValueSource DirectionValueSource { get; set; }

    [Export] public int Length { get; set; } = 1;

    /// <summary>动态长度值源，配置后覆盖 Length</summary>
    [Export] public ValueSource LengthValueSource { get; set; }

    public override Cell[] GetCells(Cell center, Context ctx) => GetCells(center, ctx, -1);

    public override Cell[] GetCells(Cell center, Context ctx, int sizeOverride)
    {
        if (center == null || ctx.Map == null) return System.Array.Empty<Cell>();
        var dir = Normalize(DirectionValueSource?.GetValue(ctx) ?? (int)Direction);
        int len = sizeOverride >= 0 ? sizeOverride : (LengthValueSource?.GetValue(ctx) ?? Length);
        if (len < 0) len = 0;

        var v = TargetResolver.CellDirectionVector(dir);
        var perp = new Vector2I(-v.Y, v.X);

        var list = new List<Cell>((len + 1) * (len + 1));
        for (int i = 0; i <= len; i++)
            for (int k = -i; k <= i; k++)
            {
                var pos = center.GridPos + v * i + perp * k;
                if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                    list.Add(c);
            }
        return list.ToArray();
    }

    public override TargetShape GetCategory() => TargetShape.Triangle;

    public override int GetAreaRange() => Length;

    private static CellDirection Normalize(int d)
        => d >= (int)CellDirection.Up && d <= (int)CellDirection.Right ? (CellDirection)d : CellDirection.Up;
}
