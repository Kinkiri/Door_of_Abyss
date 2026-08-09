using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 敌方 AI：在 EnemyAction 阶段自动驱动作战单位行动
/// 决策按关卡 AI 等级（LevelData.AiLevel）门控：简单=基础行为，标准=目标打分+移动进射程，狡诈=+威胁规避+刷怪格回避
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

    /// <summary>本回合 AI 等级（StartAITurn 时从 LevelData 读取）</summary>
    private AiLevel _aiLevel = AiLevel.标准;

    /// <summary>各单位上回合移动起点（防 A↔B 来回动：移动决策排除该格）</summary>
    private readonly Dictionary<int, Vector2I> _lastMoveFrom = new();

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
        // 玩家设置覆盖优先（settings.cfg game 段），无则用关卡配置 LevelData.AiLevel
        var overrideLevel = GameSettings.GetAiLevelOverride();
        _aiLevel = overrideLevel ?? BattleManager.Instance?.LevelData?.AiLevel ?? AiLevel.标准;
        GD.Print($"[EnemyAI] AI 等级: {_aiLevel} ({(overrideLevel.HasValue ? "玩家设置" : "关卡配置")})");

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
        {
            bm?.AIDoAttack(plan.Enemy, plan.AttackTarget);
        }
        else
        {
            // 记录移动起点（供下回合防来回：禁止移回该格）
            _lastMoveFrom[plan.Enemy.ID] = plan.Enemy.GridPos;
            bm?.AIDoMove(plan.Enemy, plan.MovePos);
        }
    }

    /// <summary>
    /// 决策单个动作（一次攻击或一次移动），返回行动计划；不执行。
    /// 按 AI 等级分流：简单=旧逻辑（最近目标+直线逼近）；标准/狡诈=战术管线。
    /// </summary>
    private AiPlan DecideAction(Unit enemy)
    {
        if (!enemy.IsAlive || enemy.IsDead || enemy.ActionPoints <= 0)
            return null;

        if (_aiLevel == AiLevel.简单)
            return DecideSimple(enemy);

        return DecideTactical(enemy);
    }

    /// <summary>简单：攻击范围内最近的玩家，否则朝最近玩家走一步（原贪心逻辑）</summary>
    private AiPlan DecideSimple(Unit enemy)
    {
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

    /// <summary>
    /// 战术决策（标准/狡诈）：
    /// 0.（狡诈）站在下回合刷怪格 → 让位优先：先移动离开（除非本回合能击杀玩家门——胜负优先）
    /// 1. 当前可攻击目标 → 打分选（门 > 一击击杀 > 高威胁 > 距离）
    /// 2. 无 → 搜索"移动后可攻击"的最佳格（AP≥2 一轮移动+攻击）；无攻击位则逼近最优目标
    ///    （标准+：威胁规避卡位——进玩家火力区的落点必须有攻击价值，否则站火力范围外等机会；
    ///     狡诈再叠加：刷怪格惩罚/排除）
    /// </summary>
    private AiPlan DecideTactical(Unit enemy)
    {
        var map = MapManager.Instance.Map;
        bool cunning = _aiLevel == AiLevel.狡诈;

        GD.Print($"[EnemyAI] 处理 {enemy.UnitData?.UnitName} 位置={enemy.GridPos} AP={enemy.ActionPoints} ({(cunning ? "狡诈" : "标准")})");

        // 威胁规避 = 标准级基础安全（避免移动进玩家火力区白送）；刷怪格回避仍狡诈专属
        HashSet<Vector2I> threatCells = null;
        if (_aiLevel != AiLevel.简单)
            threatCells = ComputeThreatCells(map);
        HashSet<Vector2I> spawnCells = null;
        if (cunning)
            spawnCells = ComputeSpawnCells();

        // 当前可攻击的玩家单位（含门）
        var attackableNow = GetAttackableUnits(enemy, enemy.GridPos, map);

        // 让位优先：站在下回合刷怪格 → 先走开（除非本回合能击杀玩家门）
        bool killDoorNow = attackableNow.Any(u => u.Type == UnitType.门 && u.CurrentHP <= enemy.AttackPower);
        if (cunning && spawnCells != null && spawnCells.Contains(enemy.GridPos) && !killDoorNow)
        {
            GD.Print($"[EnemyAI]   站在下回合刷怪格，主动让位");
            return PlanMoveAway(enemy, map, threatCells, spawnCells);
        }

        // 1. 攻击：打分选目标（攻击优先——能打到就打，不做送死预判，避免过度规避）
        var bestTarget = AiTactics.PickAttackTarget(attackableNow, enemy);
        if (bestTarget != null)
        {
            GD.Print($"[EnemyAI]   攻击目标: {bestTarget.UnitData?.UnitName}");
            return new AiPlan { Enemy = enemy, AttackTarget = bestTarget, IsAttack = true };
        }

        // 2. 移动：搜索"移动后可攻击"的位置，无则逼近最优进攻目标
        var allPlayers = UnitManager.Instance.ActiveUnits
            .Where(u => u.Team == Team.Player && u.IsAlive && !u.IsDead).ToList();
        if (allPlayers.Count == 0)
        {
            GD.Print($"[EnemyAI]   未找到玩家单位");
            return null;
        }
        var goal = AiTactics.PickAttackTarget(allPlayers, enemy);   // 最优进攻目标（门优先）
        GD.Print($"[EnemyAI]   进攻目标: {goal.UnitData?.UnitName} 在 {goal.GridPos}");

        var reachable = PathFinder.GetReachableCells(enemy.GridPos, enemy.Stamina, map);
        var attackScoreAtCell = ComputeAttackScores(enemy, reachable, map);
        // 绕障路径距离（绕墙/绕玩家占据格；队友格可穿越防自挡；忽略自身格）
        var distToGoal = PathFinder.GetDistanceFrom(goal.GridPos, map, enemy.GridPos, Team.Enemy);

        // 上回合移动起点：禁止移回（防 A↔B 来回动）
        Vector2I? prevPos = _lastMoveFrom.TryGetValue(enemy.ID, out var prev) ? prev : (Vector2I?)null;

        var movePos = AiTactics.PickBestMoveCell(
            enemy.GridPos, reachable, attackScoreAtCell, distToGoal,
            threatCells, cunning ? spawnCells : null, excludeSpawnCells: false,
            previousPos: prevPos, fallbackGoal: goal.GridPos);

        if (!movePos.HasValue)
        {
            GD.Print($"[EnemyAI]   跳过移动：可达格 {reachable.Count} 个，无可选格");
            return null;
        }

        GD.Print($"[EnemyAI]   移动到 ({movePos.Value.X},{movePos.Value.Y}) 逼近 {goal.UnitData?.UnitName}");
        return new AiPlan { Enemy = enemy, MovePos = movePos.Value, IsAttack = false };
    }

    /// <summary>让位移动：从非刷怪格中选评分最优格（仍兼顾攻击位/逼近/威胁规避）</summary>
    private AiPlan PlanMoveAway(Unit enemy, Dictionary<Vector2I, Cell> map,
        HashSet<Vector2I> threatCells, HashSet<Vector2I> spawnCells)
    {
        var allPlayers = UnitManager.Instance.ActiveUnits
            .Where(u => u.Team == Team.Player && u.IsAlive && !u.IsDead).ToList();
        if (allPlayers.Count == 0) return null;
        var goal = AiTactics.PickAttackTarget(allPlayers, enemy);

        var reachable = PathFinder.GetReachableCells(enemy.GridPos, enemy.Stamina, map);
        var attackScoreAtCell = ComputeAttackScores(enemy, reachable, map);
        var distToGoal = PathFinder.GetDistanceFrom(goal.GridPos, map, enemy.GridPos, Team.Enemy);

        // 上回合移动起点：禁止移回（防 A↔B 来回动）
        Vector2I? prevPos = _lastMoveFrom.TryGetValue(enemy.ID, out var prev) ? prev : (Vector2I?)null;

        var movePos = AiTactics.PickBestMoveCell(
            enemy.GridPos, reachable, attackScoreAtCell, distToGoal,
            threatCells, spawnCells, excludeSpawnCells: true,
            previousPos: prevPos, fallbackGoal: goal.GridPos);

        if (!movePos.HasValue || movePos.Value == enemy.GridPos)
        {
            GD.Print($"[EnemyAI]   让位失败：无可离开的格（可达格 {reachable.Count}）");
            return null;
        }

        GD.Print($"[EnemyAI]   让位移动到 ({movePos.Value.X},{movePos.Value.Y})");
        return new AiPlan { Enemy = enemy, MovePos = movePos.Value, IsAttack = false };
    }

    /// <summary>从指定位置收集可攻击的存活玩家单位</summary>
    private List<Unit> GetAttackableUnits(Unit enemy, Vector2I from, Dictionary<Vector2I, Cell> map)
    {
        var result = new List<Unit>();
        map.TryGetValue(from, out Cell cell);
        var ctx = new Context { SourceUnit = enemy, Map = map, TargetCell = cell };
        foreach (var pos in PathFinder.GetAttackableTargets(from, enemy.AttackShape, enemy.AttackDistance, enemy.Team, map, ctx))
        {
            if (!map.TryGetValue(pos, out Cell c)) continue;
            var u = c.OccupyingUnit;
            if (u != null && u.IsAlive && !u.IsDead && u.Team == Team.Player)
                result.Add(u);
        }
        return result;
    }

    /// <summary>计算每格"移动后可攻击目标"的最高得分（标准+ 走位用）；无攻击可能的格不入字典</summary>
    private Dictionary<Vector2I, int> ComputeAttackScores(Unit enemy, HashSet<Vector2I> reachable, Dictionary<Vector2I, Cell> map)
    {
        var scores = new Dictionary<Vector2I, int>();
        foreach (var pos in reachable)
        {
            map.TryGetValue(pos, out Cell cell);
            var ctx = new Context { SourceUnit = enemy, Map = map, TargetCell = cell };
            int bestScore = int.MinValue;
            foreach (var ap in PathFinder.GetAttackableTargets(pos, enemy.AttackShape, enemy.AttackDistance, enemy.Team, map, ctx))
            {
                if (!map.TryGetValue(ap, out Cell c)) continue;
                var t = c.OccupyingUnit;
                if (t == null || t.Team != Team.Player || !t.IsAlive || t.IsDead) continue;
                int s = AiTactics.ScoreTarget(enemy, t, AiTactics.ManhattanDist(pos, ap));
                if (s > bestScore) bestScore = s;
            }
            if (bestScore > int.MinValue)
                scores[pos] = bestScore;
        }
        return scores;
    }

    /// <summary>玩家攻击威胁格集（标准+：所有存活玩家单位的**火力覆盖区域**，含门，需攻击力&gt;0）</summary>
    /// <remarks>用 GetAttackRange（攻击范围内所有格）而非 GetAttackableTargets（只含当前有敌军的格）——
    /// 威胁规避必须知道"哪些格会被打"，而不是"现在谁被打"。</remarks>
    private HashSet<Vector2I> ComputeThreatCells(Dictionary<Vector2I, Cell> map)
    {
        var cells = new HashSet<Vector2I>();
        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.Team != Team.Player || !u.IsAlive || u.IsDead) continue;
            if (u.AttackPower <= 0) continue;
            map.TryGetValue(u.GridPos, out Cell cell);
            var ctx = new Context { SourceUnit = u, Map = map, TargetCell = cell };
            foreach (var pos in PathFinder.GetAttackRange(u.GridPos, u.AttackShape, u.AttackDistance, map, ctx))
                cells.Add(pos);
        }
        return cells;
    }

    /// <summary>下回合刷怪格集（狡诈：避免挡住己方援军刷新）</summary>
    private HashSet<Vector2I> ComputeSpawnCells()
    {
        var cells = new HashSet<Vector2I>();
        var bm = BattleManager.Instance;
        if (bm == null) return cells;
        foreach (var pos in bm.NextWaveSpawnPositions())
            cells.Add(pos);
        return cells;
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
