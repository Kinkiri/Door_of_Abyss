using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 敌方 AI：在 EnemyAction 阶段自动驱动作战单位行动
/// </summary>
public partial class EnemyAI : Node
{
    public static EnemyAI Instance { get; private set; }

    /// <summary>AI 单位之间行动间隔（秒）</summary>
    [Export] public float ActionDelay { get; set; } = 0.4f;

    private Queue<Unit> _actionQueue;

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[EnemyAI] 就绪");
    }

    public void Init() { }

    /// <summary>开始执行敌方回合</summary>
    public void StartAITurn()
    {
        _actionQueue = new Queue<Unit>();

        int totalEnemy = 0;
        var tempList = new List<Unit>();
        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.Team != Team.Enemy) continue;
            totalEnemy++;
            if (u.Type == UnitType.Door) continue;
            if (u.IsAlive && !u.IsDead && u.ActionPoints > 0)
                tempList.Add(u);
        }

        // 按离最近玩家门距离排序（近的先行动，攻击优先）
        var playerDoors = UnitManager.GetDoors(Team.Player).ToList();
        tempList.Sort((a, b) =>
        {
            int da = playerDoors.Count > 0 ? playerDoors.Min(d => ManhattanDist(a.GridPos, d.GridPos)) : 0;
            int db = playerDoors.Count > 0 ? playerDoors.Min(d => ManhattanDist(b.GridPos, d.GridPos)) : 0;
            return da.CompareTo(db);
        });

        string doorInfo = playerDoors.Count > 0
            ? string.Join(", ", playerDoors.Select(d => $"{d.UnitData?.UnitName}@{d.GridPos}"))
            : "无存活门";
        GD.Print($"[EnemyAI] 敌方总计 {totalEnemy}，可行动 {tempList.Count} (玩家门: {doorInfo})");

        _actionQueue = new Queue<Unit>(tempList);

        if (_actionQueue.Count == 0)
        {
            GD.Print("[EnemyAI] 无可行动敌方单位，推进阶段");
            BattleManager.Instance.AdvancePhase();
            return;
        }

        ProcessNext();
    }

    private void ProcessNext()
    {
        if (_actionQueue.Count == 0)
        {
            GD.Print("[EnemyAI] 全部处理完毕，0.3s 后推进阶段");
            var timer = GetTree().CreateTimer(0.3f);
            timer.Timeout += () => BattleManager.Instance.AdvancePhase();
            return;
        }

        var enemy = _actionQueue.Dequeue();

        // 只处理一个动作，如果还有剩余 AP 则重新入队
        bool continueAction = DoOneAction(enemy);
        if (continueAction && enemy.ActionPoints > 0)
            _actionQueue.Enqueue(enemy);

        var next = GetTree().CreateTimer(ActionDelay);
        next.Timeout += ProcessNext;
    }

    /// <summary>
    /// 执行单个动作（一次攻击或一次移动），返回 true 表示执行了动作。
    /// 贪心策略：优先攻击范围内最近的玩家，否则朝玩家走一步。
    /// </summary>
    private bool DoOneAction(Unit enemy)
    {
        if (!enemy.IsAlive || enemy.IsDead || enemy.ActionPoints <= 0)
            return false;

        var map = MapManager.Instance.Map;
        var bm = BattleManager.Instance;

        GD.Print($"[EnemyAI] 处理 {enemy.UnitData?.UnitName} 位置={enemy.GridPos} AP={enemy.ActionPoints}");

        // 1. 找攻击范围内可攻击的玩家单位
        var attackablePositions = PathFinder.GetAttackableTargets(
            enemy.GridPos, enemy.AttackDistance, enemy.Team, map);

        Unit nearestTarget = null;
        int nearestDist = int.MaxValue;

        foreach (var pos in attackablePositions)
        {
            if (!map.TryGetValue(pos, out Cell c)) continue;
            var occupant = c.OccupyingUnit;
            if (occupant == null || occupant.Team != Team.Player || !occupant.IsAlive) continue;

            int dist = ManhattanDist(enemy.GridPos, occupant.GridPos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestTarget = occupant;
            }
        }

        if (nearestTarget != null)
        {
            GD.Print($"[EnemyAI]   攻击目标: {nearestTarget.UnitData?.UnitName}");
            bm.AIDoAttack(enemy, nearestTarget);
            return true;
        }

        // 2. 无法攻击 → 沿最短路径朝最近玩家走一步
        var nearestPlayer = FindNearestPlayer(enemy.GridPos);
        if (nearestPlayer == null)
        {
            GD.Print($"[EnemyAI]   未找到玩家单位");
            return false;
        }

        GD.Print($"[EnemyAI]   最近玩家: {nearestPlayer.UnitData?.UnitName} 在 {nearestPlayer.GridPos}");

        // 找所有可达格子，选离目标最近的（不走玩家站着的格，因为 CanStand=false）
        var reachable = PathFinder.GetReachableCells(
            enemy.GridPos, enemy.Stamina, map);

        Vector2I? bestMove = null;
        int bestDist = int.MaxValue;
        foreach (var pos in reachable)
        {
            if (pos == enemy.GridPos) continue;
            if (!map.TryGetValue(pos, out Cell c) || c.OccupyingUnit != null) continue;
            int dist = ManhattanDist(pos, nearestPlayer.GridPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestMove = pos;
            }
        }

        if (!bestMove.HasValue)
        {
            GD.Print($"[EnemyAI]   无可达格子（体力={enemy.Stamina}）");
            return false;
        }

        GD.Print($"[EnemyAI]   移动到 ({bestMove.Value.X},{bestMove.Value.Y}) 距离玩家 {bestDist}");
        bm.AIDoMove(enemy, bestMove.Value);
        return true;
    }

    private Unit FindNearestPlayer(Vector2I from)
    {
        Unit nearest = null;
        int minDist = int.MaxValue;

        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.Team != Team.Player || !u.IsAlive || u.IsDead) continue;
            int dist = ManhattanDist(from, u.GridPos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = u;
            }
        }

        return nearest;
    }

    private static int ManhattanDist(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }
}
