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

    /// <summary>行动前镜头预告停顿（秒）：先让摄像机飞向行动位置，再执行行动（保证远距离行动可见）</summary>
    [Export] public float CameraPanDelay { get; set; } = 0.4f;

    /// <summary>AI 攻击行动预告（行动前发出，View 层订阅，如摄像机跟随）；参数：攻击者 + 攻击目标</summary>
    public event System.Action<Unit, Unit> AiAttackPreviewed;

    /// <summary>AI 移动行动预告（行动前发出，View 层订阅）；参数：移动单位 + 目标格</summary>
    public event System.Action<Unit, Vector2I> AiMovePreviewed;

    /// <summary>AI 单次行动计划（决策与执行分离：决策后预告镜头，停顿后再执行）</summary>
    private class AiPlan
    {
        public Unit Enemy;
        public Unit AttackTarget;   // Kind=Attack
        public Vector2I MovePos;    // Kind=Move
        public bool IsAttack;
    }

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
            if (u.Type == UnitType.门) continue;
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
            // processAlways:false —— 树暂停（Esc 暂停）时 AI 计时器停止，真实暂停
            var timer = GetTree().CreateTimer(0.3f, processAlways: false);
            timer.Timeout += () => BattleManager.Instance.AdvancePhase();
            return;
        }

        var enemy = _actionQueue.Dequeue();

        // 决策（纯读）→ 预告镜头 → 停顿让摄像机跑过去 → 再执行行动（保证远距离行动全程可见）
        var plan = DecideAction(enemy);
        if (plan == null)
        {
            // 无可行动作（找不到玩家等）：直接下一个
            var next = GetTree().CreateTimer(ActionDelay, processAlways: false);
            next.Timeout += ProcessNext;
            return;
        }

        PreviewCamera(plan);
        var pan = GetTree().CreateTimer(CameraPanDelay, processAlways: false);
        pan.Timeout += () =>
        {
            ExecutePlan(plan);
            // 只处理一个动作，如果还有剩余 AP 则重新入队
            if (enemy.ActionPoints > 0)
                _actionQueue.Enqueue(enemy);
            var next = GetTree().CreateTimer(ActionDelay, processAlways: false);
            next.Timeout += ProcessNext;
        };
    }

    /// <summary>预告摄像机（发事件，View 层订阅驱动镜头）：攻击 → 行动单位+目标单位中点；移动 → 行动单位+目标格中点</summary>
    private void PreviewCamera(AiPlan plan)
    {
        if (plan.IsAttack)
            AiAttackPreviewed?.Invoke(plan.Enemy, plan.AttackTarget);
        else
            AiMovePreviewed?.Invoke(plan.Enemy, plan.MovePos);
    }

    /// <summary>执行已决策的行动（停顿结束后调用）</summary>
    private void ExecutePlan(AiPlan plan)
    {
        var bm = BattleManager.Instance;
        if (plan.IsAttack)
            bm?.AIDoAttack(plan.Enemy, plan.AttackTarget);
        else
            bm?.AIDoMove(plan.Enemy, plan.MovePos);
    }

    /// <summary>
    /// 决策单个动作（一次攻击或一次移动），返回行动计划；不执行。
    /// 贪心策略：优先攻击范围内最近的玩家，否则朝玩家走一步。
    /// </summary>
    private AiPlan DecideAction(Unit enemy)
    {
        if (!enemy.IsAlive || enemy.IsDead || enemy.ActionPoints <= 0)
            return null;

        var map = MapManager.Instance.Map;

        GD.Print($"[EnemyAI] 处理 {enemy.UnitData?.UnitName} 位置={enemy.GridPos} AP={enemy.ActionPoints}");

        // 1. 找攻击范围内可攻击的玩家单位
        map.TryGetValue(enemy.GridPos, out Cell enemyCell);
        var atkCtx = new Context { SourceUnit = enemy, Map = map, TargetCell = enemyCell };
        var attackablePositions = PathFinder.GetAttackableTargets(
            enemy.GridPos, enemy.AttackShape, enemy.AttackDistance, enemy.Team, map, atkCtx);

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
            return new AiPlan { Enemy = enemy, AttackTarget = nearestTarget, IsAttack = true };
        }

        // 2. 无法攻击 → 沿最短路径朝最近玩家走一步
        var nearestPlayer = FindNearestPlayer(enemy.GridPos);
        if (nearestPlayer == null)
        {
            GD.Print($"[EnemyAI]   未找到玩家单位");
            return null;
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
            return null;
        }

        GD.Print($"[EnemyAI]   移动到 ({bestMove.Value.X},{bestMove.Value.Y}) 距离玩家 {bestDist}");
        return new AiPlan { Enemy = enemy, MovePos = bestMove.Value, IsAttack = false };
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
