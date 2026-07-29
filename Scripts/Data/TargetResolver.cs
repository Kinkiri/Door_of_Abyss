using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 目标解析器：根据 TargetShape + TargetFilter 将单个目标扩散为多目标列表。
/// 供 GameAction 入口调用，让 Damage/Heal 循环处理多个目标。
/// </summary>
public static class TargetResolver
{
    /// <summary>
    /// 解析目标列表
    /// </summary>
    /// <param name="shape">范围形状</param>
    /// <param name="filter">阵营过滤</param>
    /// <param name="source">效果来源单位（用于位置/阵营参考）</param>
    /// <param name="singleTarget">玩家点选的单个单位</param>
    /// <param name="centerCell">范围攻击的中心格子</param>
    /// <param name="areaRange">AreaDiamond/AreaSquare 扩散半径</param>
    /// <returns>目标单位数组，无目标返回空数组</returns>
    public static Unit[] Resolve(
        TargetShape shape,
        TargetFilter filter,
        Unit source,
        Unit singleTarget,
        Cell centerCell,
        Team sourceTeam,
        int areaRange = 1)
    {
        Team? teamFilter = filter switch
        {
            TargetFilter.All => null,
            TargetFilter.Enemy => sourceTeam == Team.Player ? Team.Enemy : Team.Player,
            TargetFilter.Ally => sourceTeam,
            _ => null,
        };

        switch (shape)
        {
            case TargetShape.None:
                return Array.Empty<Unit>();

            case TargetShape.SingleCell:
                return null; // 格子目标不走单位

            case TargetShape.SingleUnit:
                return singleTarget != null ? new[] { singleTarget } : null;

            case TargetShape.AreaDiamond:
                return ResolveCellArea(centerCell, areaRange, teamFilter, isDiamond: true);

            case TargetShape.AreaSquare:
                return ResolveCellArea(centerCell, areaRange, teamFilter, isDiamond: false);

            case TargetShape.All:
                return FilterTeam(teamFilter);

            default:
                return singleTarget != null ? new[] { singleTarget } : null;
        }
    }

    /// <summary>按阵营筛选存活单位</summary>
    private static Unit[] FilterTeam(Team? team)
    {
        var units = UnitManager.Instance.ActiveUnits;
        var list = new List<Unit>(units.Count);
        foreach (var u in units)
        {
            if (!u.IsAlive || u.IsDead) continue;
            if (team != null && u.Team != team) continue;
            list.Add(u);
        }
        return list.ToArray();
    }

    /// <summary>格子范围扩散，找格子上的单位，可选按阵营过滤</summary>
    private static Unit[] ResolveCellArea(Cell center, int range, Team? teamFilter, bool isDiamond)
    {
        if (center == null) return null;

        var map = MapManager.Instance?.Map;
        if (map == null) return null;

        var list = new List<Unit>();
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (isDiamond && Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;
                var pos = new Vector2I(center.GridPos.X + dx, center.GridPos.Y + dy);
                if (map.TryGetValue(pos, out Cell c) && c.OccupyingUnit != null)
                {
                    var u = c.OccupyingUnit;
                    if (!u.IsAlive || u.IsDead) continue;
                    if (teamFilter != null && u.Team != teamFilter) continue;
                    list.Add(u);
                }
            }
        }
        return list.ToArray();
    }
}
