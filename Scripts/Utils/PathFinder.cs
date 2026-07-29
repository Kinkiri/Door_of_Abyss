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
    /// 攻击范围从起点独立计算，与移动范围无关。
    /// </summary>
    public static HashSet<Vector2I> GetReachableCellsWithAttackTargets(
        Vector2I start,
        int movementPoints,
        int attackDistance,
        Team attackingTeam,
        Dictionary<Vector2I, Cell> map,
        out HashSet<Vector2I> attackableTargets)
    {
        HashSet<Vector2I> reachable = GetReachableCells(start, movementPoints, map);
        attackableTargets = GetAttackableTargets(start, attackDistance, attackingTeam, map);
        return reachable;
    }

    /// <summary>
    /// 独立计算从指定位置可攻击到的敌方单位。
    /// </summary>
    public static HashSet<Vector2I> GetAttackableTargets(
        Vector2I pos,
        int attackDistance,
        Team attackingTeam,
        Dictionary<Vector2I, Cell> map)
    {
        var targets = new HashSet<Vector2I>();

        for (int dy = -attackDistance; dy <= attackDistance; dy++)
        {
            for (int dx = -attackDistance; dx <= attackDistance; dx++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > attackDistance)
                    continue;

                Vector2I checkPos = new Vector2I(pos.X + dx, pos.Y + dy);

                if (!map.TryGetValue(checkPos, out Cell cell))
                    continue;

                Unit enemy = cell.OccupyingUnit;
                if (enemy == null || !enemy.CanBeAttacked || enemy.Team == attackingTeam)
                    continue;

                targets.Add(checkPos);
            }
        }

        return targets;
    }

    /// <summary>
    /// 返回以 pos 为中心、曼哈顿距离 range 内的所有格子坐标（不含自身）
    /// </summary>
    public static HashSet<Vector2I> GetCellsInRange(
        Vector2I pos,
        int range,
        Dictionary<Vector2I, Cell> map)
    {
        var cells = new HashSet<Vector2I>();
        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;
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
