using Godot;
using System.Collections.Generic;

/// <summary>
/// 随机格坐标值源：在指定形状（CellShape，如菱形/方形/十字/射线/三角形）内随机取一格。
/// RequireStandable=true（默认）时只从"可站立且未被占据"的格子中随机（召唤落点用）；
/// 无可选格返回 null。Shape 为 null 时返回 null（策划需显式配置形状）。
/// </summary>
[GlobalClass]
public partial class RandomCellValue : CellValueSource
{
    /// <summary>中心基准（null = 回退 ctx.TargetCell；仍为 null 则无有效坐标）</summary>
    [Export] public CellValueSource Base { get; set; }

    /// <summary>随机范围形状（null = 无有效坐标）</summary>
    [Export] public CellShape Shape { get; set; }

    /// <summary>true=只取可站立且未被占据的格子（默认），false=形状内任意格子</summary>
    [Export] public bool RequireStandable { get; set; } = true;

    public override Vector2I? GetCell(Context ctx)
    {
        var map = ctx.Map ?? MapManager.Instance?.Map;
        if (map == null || Shape == null) return null;

        Cell center;
        if (Base != null)
        {
            var pos = Base.GetCell(ctx);
            if (pos == null) return null;
            map.TryGetValue(pos.Value, out center);
        }
        else
        {
            center = ctx.TargetCell;
        }
        if (center == null) return null;

        var candidates = Shape.GetCells(center, ctx);
        if (RequireStandable)
        {
            var list = new List<Cell>(candidates.Length);
            foreach (var c in candidates)
                if (c.CanStand && c.OccupyingUnit == null)
                    list.Add(c);
            candidates = list.ToArray();
        }

        if (candidates.Length == 0) return null;
        return candidates[GD.RandRange(0, candidates.Length - 1)].GridPos;
    }
}
