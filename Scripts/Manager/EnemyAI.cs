using Godot;
using System.Collections.Generic;

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
        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.Team != Team.Enemy) continue;
            totalEnemy++;
            // 门（水晶）不由 AI 操控移动
            if (u.Type == UnitType.Door) continue;
            if (u.IsAlive && !u.IsDead && u.ActionPoints > 0)
            {
                _actionQueue.Enqueue(u);
                GD.Print($"[EnemyAI] 入队: {u.UnitData?.UnitName} ID={u.ID} 位置={u.GridPos} AP={u.ActionPoints}");
            }
            else
            {
                GD.Print($"[EnemyAI] 跳过: {u.UnitData?.UnitName} ID={u.ID} 原因={(u.IsAlive ? "" : "死亡")} {(u.IsDead ? "IsDead" : "")} AP={u.ActionPoints}");
            }
        }

        GD.Print($"[EnemyAI] 敌方总计 {totalEnemy}，可行动 {_actionQueue.Count}");

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

        // 2. 无法攻击 → 朝最近玩家移动
        var reachable = PathFinder.GetReachableCells(
            enemy.GridPos, enemy.RemainingStamina, map);

        if (reachable.Count == 0) return false;

        var nearestPlayer = FindNearestPlayer(enemy.GridPos);
        if (nearestPlayer == null) return false;

        Vector2I? bestMove = null;
        int bestDist = int.MaxValue;
        foreach (var pos in reachable)
        {
            if (pos == enemy.GridPos) continue;
            int dist = ManhattanDist(pos, nearestPlayer.GridPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestMove = pos;
            }
        }

        if (!bestMove.HasValue) return false;

        GD.Print($"[EnemyAI]   移动到 ({bestMove.Value.X}, {bestMove.Value.Y})");
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
