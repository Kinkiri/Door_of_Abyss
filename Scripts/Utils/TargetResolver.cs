using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 目标解析器：TargetFilter 组合树的静态入口 + 共享候选算法（纯函数，战场数据由 ctx 传入）。
/// ResolveUnits/ResolveCells 以 null 候选调用 filter（形状节点自生成、过滤节点从全量开始）。
/// </summary>
public static class TargetResolver
{
    /// <summary>解析单位目标；filter 为 null 时返回空数组</summary>
    public static Unit[] ResolveUnits(TargetFilter filter, Context ctx)
        => filter?.ApplyUnits(null, ctx) ?? Array.Empty<Unit>();

    /// <summary>解析格子目标；filter 为 null 时返回空数组</summary>
    public static Cell[] ResolveCells(TargetFilter filter, Context ctx)
        => filter?.ApplyCells(null, ctx) ?? Array.Empty<Cell>();

    /// <summary>单位是否可作为目标（存活且未标记死亡）</summary>
    public static bool IsValidTarget(Unit u) => u != null && u.IsAlive && !u.IsDead;

    /// <summary>全部存活单位</summary>
    public static Unit[] AllAliveUnits(List<Unit> activeUnits)
    {
        if (activeUnits == null) return Array.Empty<Unit>();
        var list = new List<Unit>(activeUnits.Count);
        foreach (var u in activeUnits)
            if (IsValidTarget(u))
                list.Add(u);
        return list.ToArray();
    }

    /// <summary>地图全部格子</summary>
    public static Cell[] AllCells(Dictionary<Vector2I, Cell> map)
    {
        if (map == null) return Array.Empty<Cell>();
        var list = new List<Cell>(map.Count);
        foreach (var c in map.Values)
            if (c != null)
                list.Add(c);
        return list.ToArray();
    }

    /// <summary>区域内格子上的存活单位（菱形/方形扩散）</summary>
    public static Unit[] UnitsInArea(Cell center, int range, bool diamond, Dictionary<Vector2I, Cell> map)
    {
        var cells = CellsInArea(center, range, diamond, map);
        var list = new List<Unit>(cells.Length);
        foreach (var c in cells)
        {
            var u = c.OccupyingUnit;
            if (IsValidTarget(u))
                list.Add(u);
        }
        return list.ToArray();
    }

    /// <summary>区域内格子（菱形/方形扩散，含中心格）</summary>
    public static Cell[] CellsInArea(Cell center, int range, bool diamond, Dictionary<Vector2I, Cell> map)
    {
        if (center == null || map == null) return Array.Empty<Cell>();
        var list = new List<Cell>();
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (diamond && Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;
                var pos = new Vector2I(center.GridPos.X + dx, center.GridPos.Y + dy);
                if (map.TryGetValue(pos, out Cell c) && c != null)
                    list.Add(c);
            }
        }
        return list.ToArray();
    }

    /// <summary>从格子集合提取其上的存活单位（CellShape 的 ApplyUnits 用）</summary>
    public static Unit[] UnitsFromCells(Cell[] cells)
    {
        if (cells == null) return Array.Empty<Unit>();
        var list = new List<Unit>(cells.Length);
        foreach (var c in cells)
        {
            var u = c?.OccupyingUnit;
            if (IsValidTarget(u))
                list.Add(u);
        }
        return list.ToArray();
    }

    /// <summary>4 向单位向量（曼哈顿约定，与 StepCellValue/MoveUnitAction 一致）</summary>
    public static Vector2I CellDirectionVector(CellDirection d) => d switch
    {
        CellDirection.Up => new Vector2I(0, -1),
        CellDirection.Down => new Vector2I(0, 1),
        CellDirection.Left => new Vector2I(-1, 0),
        CellDirection.Right => new Vector2I(1, 0),
        _ => Vector2I.Zero,
    };

    /// <summary>
    /// 计算 from → to 的曼哈顿方向（4 向）：|dx| ≥ |dy| 取横向（dx>0→Right，dx<0→Left），否则纵向（dy>0→Down，dy<0→Up）；
    /// 同格（无位移）返回 null。与 DirectionValue/MoveUnitAction 同约定。
    /// </summary>
    public static CellDirection? DirectionBetween(Vector2I from, Vector2I to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return null;
        if (System.Math.Abs(dx) >= System.Math.Abs(dy))
            return dx >= 0 ? CellDirection.Right : CellDirection.Left;
        return dy >= 0 ? CellDirection.Down : CellDirection.Up;
    }
}
