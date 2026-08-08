using Godot;
using System.Collections.Generic;

/// <summary>
/// BFS 寻路工具，计算单位在当前体力下可到达的格子集合 和 最短路径
/// </summary>
public static class PathFinder
{
    /// <summary>
    /// 返回从起点出发、在 movementPoints 体力内可到达的所有格子坐标。
    /// 中途经过的格子只需 CanPass（可穿越），目标格还需 CanStand（可站立）。
    /// </summary>
    public static HashSet<Vector2I> GetReachableCells(
        Vector2I start,
        int movementPoints,
        Dictionary<Vector2I, Cell> map)
    {
        var result = new HashSet<Vector2I>();
        var visited = new Dictionary<Vector2I, int>();
        var queue = new Queue<(Vector2I pos, int cost)>();

        queue.Enqueue((start, 0));
        visited[start] = 0;

        while (queue.Count > 0)
        {
            var (pos, cost) = queue.Dequeue();

            foreach (Vector2I dir in _directions)
            {
                Vector2I next = pos + dir;

                if (!map.TryGetValue(next, out Cell cell))
                    continue;
                if (!cell.CanPass)
                    continue;

                int nextCost = cost + cell.MoveCost;
                if (nextCost > movementPoints)
                    continue;

                if (visited.TryGetValue(next, out int prevCost) && prevCost <= nextCost)
                    continue;

                visited[next] = nextCost;

                // 能穿越 + 能站立才算可到达（可停留）
                if (cell.CanStand)
                    result.Add(next);

                queue.Enqueue((next, nextCost));
            }
        }

        return result;
    }

    /// <summary>
    /// 弃用！
    /// 返回从起点到目标点的最短路径（含起点和目标点）。
    /// 中途格子只需 CanPass（可穿越），目标格还需 CanStand（可站立）。
    /// 体力不够、不可穿越、不可站立时返回空列表。
    /// </summary>
    public static List<Vector2I> GetShortestPath(
        Vector2I start,
        Vector2I target,
        int movementPoints,
        Dictionary<Vector2I, Cell> map)
    {
        if (start == target)
            return new List<Vector2I> { start };

        // 目标格必须先检查是否存在且可站立
        if (!map.TryGetValue(target, out Cell targetCell) || !targetCell.CanStand)
            return new List<Vector2I>();

        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var visited = new Dictionary<Vector2I, int>();
        var queue = new Queue<(Vector2I pos, int cost)>();

        queue.Enqueue((start, 0));
        visited[start] = 0;

        while (queue.Count > 0)
        {
            var (pos, cost) = queue.Dequeue();

            foreach (Vector2I dir in _directions)
            {
                Vector2I next = pos + dir;

                if (!map.TryGetValue(next, out Cell cell))
                    continue;
                if (!cell.CanPass)
                    continue;

                int nextCost = cost + cell.MoveCost;
                if (nextCost > movementPoints)
                    continue;

                if (visited.TryGetValue(next, out int prevCost) && prevCost <= nextCost)
                    continue;

                visited[next] = nextCost;
                cameFrom[next] = pos;
                queue.Enqueue((next, nextCost));
            }
        }

        // 没找到目标点
        if (!cameFrom.ContainsKey(target))
            return new List<Vector2I>();

        // 反向回溯路径
        var path = new List<Vector2I>();
        Vector2I current = target;
        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 计算移动范围的同时，找出从当前位置可攻击到的敌方单位位置。
    /// 攻击范围从起点独立计算，与移动范围无关。shape=null 时用默认菱形（attackDistance 半径）。
    /// </summary>
    public static HashSet<Vector2I> GetReachableCellsWithAttackTargets(
        Vector2I start,
        int movementPoints,
        CellShape shape,
        int attackDistance,
        Team attackingTeam,
        Dictionary<Vector2I, Cell> map,
        Context ctx,
        out HashSet<Vector2I> attackableTargets)
    {
        HashSet<Vector2I> reachable = GetReachableCells(start, movementPoints, map);
        attackableTargets = GetAttackableTargets(start, shape, attackDistance, attackingTeam, map, ctx);
        return reachable;
    }

    /// <summary>
    /// 从目标点做 BFS，计算地图上各格到目标的绕障最短成本（累加 MoveCost）。
    /// 返回 格 → 成本；不可达格不在字典中。用于 AI 移动评分（按实际路径绕路，而非直线距离）。
    /// ignorePos：视为可穿越的格（如 AI 单位自身当前格——它将离开该格，不应挡住自己的路径）。
    /// passThroughTeam：该阵营的**占据格**视为可穿越（如 AI 队友——它们会移动/让路，不应把己方援军挡成死路）；
    /// 无单位占据的墙（CanPass=false）与其他阵营占据格仍为障碍。
    /// </summary>
    public static Dictionary<Vector2I, int> GetDistanceFrom(
        Vector2I target,
        Dictionary<Vector2I, Cell> map,
        Vector2I? ignorePos = null,
        Team? passThroughTeam = null)
    {
        var dist = new Dictionary<Vector2I, int>();
        var queue = new Queue<(Vector2I pos, int cost)>();

        queue.Enqueue((target, 0));
        dist[target] = 0;

        while (queue.Count > 0)
        {
            var (pos, cost) = queue.Dequeue();

            foreach (Vector2I dir in _directions)
            {
                Vector2I next = pos + dir;

                if (!map.TryGetValue(next, out Cell cell))
                    continue;

                bool canPass = cell.CanPass;
                if (!canPass)
                {
                    if (ignorePos.HasValue && next == ignorePos.Value) canPass = true;
                    else if (passThroughTeam.HasValue && cell.OccupyingUnit != null
                             && cell.OccupyingUnit.Team == passThroughTeam.Value) canPass = true;
                }
                if (!canPass) continue;

                int nextCost = cost + cell.MoveCost;
                if (dist.TryGetValue(next, out int prevCost) && prevCost <= nextCost)
                    continue;

                dist[next] = nextCost;
                queue.Enqueue((next, nextCost));
            }
        }

        return dist;
    }

    /// <summary>
    /// 独立计算从指定位置可攻击到的敌方单位。
    /// shape=null 时用默认菱形（attackDistance 半径）；shape 非空时主尺寸联动 attackDistance（=单位射程）。
    /// </summary>
    public static HashSet<Vector2I> GetAttackableTargets(
        Vector2I pos,
        CellShape shape,
        int attackDistance,
        Team attackingTeam,
        Dictionary<Vector2I, Cell> map,
        Context ctx)
    {
        var targets = new HashSet<Vector2I>();

        foreach (Vector2I checkPos in GetAttackRange(pos, shape, attackDistance, map, ctx))
        {
            if (!map.TryGetValue(checkPos, out Cell cell))
                continue;

            Unit enemy = cell.OccupyingUnit;
            if (enemy == null || !enemy.CanBeAttacked || enemy.Team == attackingTeam)
                continue;

            targets.Add(checkPos);
        }

        return targets;
    }

    /// <summary>
    /// 生成攻击范围格子（**不含自身**）：
    /// - shape=null → 默认菱形（曼哈顿距离 fallbackRange 内，兼容旧逻辑）
    /// - shape 非空 → shape.GetCells(center, ctx, fallbackRange)（主尺寸联动射程；center 须在地图内，否则返回空）
    /// </summary>
    public static HashSet<Vector2I> GetAttackRange(
        Vector2I pos,
        CellShape shape,
        int fallbackRange,
        Dictionary<Vector2I, Cell> map,
        Context ctx)
    {
        var cells = new HashSet<Vector2I>();

        if (shape != null)
        {
            if (map == null || !map.TryGetValue(pos, out Cell center) || center == null)
                return cells;
            var shapeCells = shape.GetCells(center, ctx, fallbackRange);
            foreach (var c in shapeCells)
                if (c != null && c.GridPos != pos)
                    cells.Add(c.GridPos);
            return cells;
        }

        if (map == null) return cells;
        for (int dy = -fallbackRange; dy <= fallbackRange; dy++)
        {
            for (int dx = -fallbackRange; dx <= fallbackRange; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > fallbackRange) continue;
                Vector2I checkPos = new Vector2I(pos.X + dx, pos.Y + dy);
                if (map.ContainsKey(checkPos))
                    cells.Add(checkPos);
            }
        }
        return cells;
    }

    /// <summary>四方向偏移</summary>
    private static readonly Vector2I[] _directions =
    {
        Vector2I.Up,
        Vector2I.Down,
        Vector2I.Left,
        Vector2I.Right,
    };
}
