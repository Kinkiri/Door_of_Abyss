using Godot;
using System.Collections.Generic;

/// <summary>
/// 射线形状：沿 Direction 方向的矩形带（平头）。
/// 含中心排（起点），共 Length+1 排；每排垂直于方向的宽度为 2×Width+1（Width=0 → 单格宽）。
/// 例：Up、Length=2、Width=1 → 3 排 × 3 宽 = 9 格。
/// </summary>
[GlobalClass]
public partial class RayShape : CellShape
{
    [Export] public CellDirection Direction { get; set; } = CellDirection.Up;

    /// <summary>动态方向值源（如 DirectionValue 计算朝向目标），配置后覆盖 Direction；非法值按 Up 处理</summary>
    [Export] public ValueSource DirectionValueSource { get; set; }

    [Export] public int Length { get; set; } = 1;

    /// <summary>动态长度值源，配置后覆盖 Length</summary>
    [Export] public ValueSource LengthValueSource { get; set; }

    [Export] public int Width { get; set; } = 0;

    /// <summary>动态宽度值源，配置后覆盖 Width</summary>
    [Export] public ValueSource WidthValueSource { get; set; }

    public override Cell[] GetCells(Cell center, Context ctx) => GetCells(center, ctx, -1);

    public override Cell[] GetCells(Cell center, Context ctx, int sizeOverride)
    {
        if (center == null || ctx.Map == null) return System.Array.Empty<Cell>();
        var dir = Normalize(DirectionValueSource?.GetValue(ctx) ?? (int)Direction);
        int len = sizeOverride >= 0 ? sizeOverride : (LengthValueSource?.GetValue(ctx) ?? Length);
        int w = WidthValueSource?.GetValue(ctx) ?? Width;
        if (len < 0) len = 0;
        if (w < 0) w = 0;

        var v = TargetResolver.CellDirectionVector(dir);
        var perp = new Vector2I(-v.Y, v.X);

        var list = new List<Cell>((len + 1) * (w * 2 + 1));
        for (int i = 0; i <= len; i++)
            for (int k = -w; k <= w; k++)
            {
                var pos = center.GridPos + v * i + perp * k;
                if (ctx.Map.TryGetValue(pos, out Cell c) && c != null)
                    list.Add(c);
            }
        return list.ToArray();
    }

    public override TargetShape GetCategory() => TargetShape.Ray;

    public override int GetAreaRange() => Length;

    private static CellDirection Normalize(int d)
        => d >= (int)CellDirection.Up && d <= (int)CellDirection.Right ? (CellDirection)d : CellDirection.Up;
}
