using Godot;
using System.Collections.Generic;

/// <summary>
/// 敌人 AI 战术决策纯逻辑（静态工具，零 Manager 依赖，可单测）。
/// 调用方（EnemyAI）负责收集战场数据（可达格/威胁格/刷怪格/候选目标）后传入。
/// </summary>
public static class AiTactics
{
    // 目标价值权重：门（胜负关键）> 一击可杀 > 高攻击威胁 > 距离惩罚
    private const int DoorScore = 1000;
    private const int OneShotScore = 300;
    private const int ThreatPerAttack = 2;

    // 移动评分惩罚：进入玩家攻击范围（可攻击时轻罚=值得冒险，无攻击价值重罚=白送）、站下回合刷怪格
    private const int MoveIntoThreatPenalty = 200;
    private const int StandInThreatPenalty = 500;
    private const int SpawnCellPenalty = 150;

    /// <summary>
    /// 目标价值打分：门 > 一击可杀 > 高攻击威胁，再减距离惩罚。
    /// distance &lt; 0 时用 attacker 当前位置到目标的曼哈顿距离。
    /// </summary>
    public static int ScoreTarget(Unit attacker, Unit target, int distance = -1)
    {
        if (attacker == null || target == null || !target.IsAlive || target.IsDead)
            return int.MinValue;

        if (distance < 0)
            distance = ManhattanDist(attacker.GridPos, target.GridPos);

        int score = 0;
        if (target.Type == UnitType.门)
            score += DoorScore;
        else if (target.CurrentHP <= attacker.AttackPower)
            score += OneShotScore;
        score += target.AttackPower * ThreatPerAttack;
        score -= distance;
        return score;
    }

    /// <summary>从候选单位中选得分最高的攻击目标（无有效目标返回 null）</summary>
    public static Unit PickAttackTarget(IEnumerable<Unit> candidates, Unit attacker)
    {
        if (candidates == null || attacker == null) return null;

        Unit best = null;
        int bestScore = int.MinValue;
        foreach (var t in candidates)
        {
            if (t == null || t.Team == attacker.Team) continue;
            int s = ScoreTarget(attacker, t);
            if (s > bestScore)
            {
                bestScore = s;
                best = t;
            }
        }
        return best;
    }

    /// <summary>
    /// 攻击是否会送死：打不死目标（当前攻击力不足以击杀）且攻击后会被存活玩家单位一击反杀。
    /// 门除外（胜负关键，送死值得）。攻击不改变位置，故按 attacker 当前格判断反杀覆盖。
    /// </summary>
    /// <param name="playerUnits">全部存活玩家单位（含门）</param>
    public static bool IsSuicidalAttack(Unit attacker, Unit target,
        IEnumerable<Unit> playerUnits, Dictionary<Vector2I, Cell> map)
    {
        if (attacker == null || target == null) return false;
        if (attacker.AttackPower >= target.CurrentHP) return false;   // 一击击杀 → 不亏
        if (target.Type == UnitType.门) return false;                  // 打门值得（胜负关键）

        foreach (var u in playerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            if (u.Team == attacker.Team) continue;
            if (u.AttackPower < attacker.CurrentHP) continue;          // 一击杀不死 attacker
            if (map == null) continue;
            map.TryGetValue(u.GridPos, out Cell cell);
            var ctx = new Context { SourceUnit = u, Map = map, TargetCell = cell };
            if (PathFinder.GetAttackableTargets(u.GridPos, u.AttackShape, u.AttackDistance, u.Team, map, ctx)
                .Contains(attacker.GridPos))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 移动格评分选择（纯函数）：返回评分最高的移动目标格；无可选格返回 null（跳过行动）。
    /// 评分 = 移动后可攻击目标得分（attackScoreAtCell，不含=0）
    ///       − 到进攻目标的绕障步数（distToGoal，不可达格直接跳过）
    ///       − 威胁格惩罚（threatenedCells 非空时生效）
    ///       − 刷怪格惩罚（spawnCells 非空且未排除时生效）
    /// 防"来回动"三件套：
    ///   previousPos（上回合起点）→ 禁止直接回头；
    ///   maxDetour（默认 3）→ 绕远超过该值的候选跳过（防无限走丢）；
    ///   候选格必须存在才返回（无候选不动）。
    /// </summary>
    /// <param name="currentPos">单位当前格（不会选中自身）</param>
    /// <param name="reachable">可达格集合（含自身）</param>
    /// <param name="attackScoreAtCell">每格"移动后可攻击目标"的最高得分（不含=不能攻击）</param>
    /// <param name="distToGoal">每格到进攻目标的绕障成本（PathFinder.GetDistanceFrom；不含=不可达）</param>
    /// <param name="threatenedCells">玩家攻击威胁格集（null=不规避）</param>
    /// <param name="spawnCells">下回合刷怪格集（null=不回避）</param>
    /// <param name="excludeSpawnCells">true=完全排除刷怪格（主动让位），false=仅惩罚</param>
    /// <param name="previousPos">上回合移动起点（禁止移回该格，防 A↔B 来回）</param>
    /// <param name="maxDetour">允许绕远的步数上限（候选 dist 不得超过当前格 dist + 该值，防无限走丢）</param>
    public static Vector2I? PickBestMoveCell(
        Vector2I currentPos,
        IReadOnlyCollection<Vector2I> reachable,
        IReadOnlyDictionary<Vector2I, int> attackScoreAtCell,
        IReadOnlyDictionary<Vector2I, int> distToGoal,
        IReadOnlySet<Vector2I> threatenedCells,
        IReadOnlySet<Vector2I> spawnCells,
        bool excludeSpawnCells = false,
        Vector2I? previousPos = null,
        int maxDetour = 3)
    {
        if (reachable == null || distToGoal == null) return null;

        // 目标不可达当前格 → 移动无意义（避免乱走/来回），跳过行动
        if (!distToGoal.TryGetValue(currentPos, out int curDist))
            return null;

        Vector2I? best = null;
        int bestScore = int.MinValue;

        foreach (var pos in reachable)
        {
            if (pos == currentPos) continue;
            if (excludeSpawnCells && spawnCells != null && spawnCells.Contains(pos)) continue;
            if (previousPos.HasValue && pos == previousPos.Value) continue;   // 禁止直接回头
            if (!distToGoal.TryGetValue(pos, out int dist)) continue;   // 绕障不可达目标 → 去了没用
            if (dist > curDist + maxDetour) continue;                   // 绕远过猛 → 防走丢

            int score = 0;
            int atkScore = 0;
            if (attackScoreAtCell != null && attackScoreAtCell.TryGetValue(pos, out atkScore))
                score += atkScore;
            score -= dist;
            if (threatenedCells != null && threatenedCells.Contains(pos))
            {
                // 卡位：进玩家火力区的落点必须有攻击价值（打门/收割/高威胁），否则重罚（白送挨打）
                score -= atkScore > 0 ? MoveIntoThreatPenalty : StandInThreatPenalty;
            }
            if (spawnCells != null && spawnCells.Contains(pos))
                score -= SpawnCellPenalty;

            if (score > bestScore)
            {
                bestScore = score;
                best = pos;
            }
        }

        return best;
    }

    /// <summary>曼哈顿距离</summary>
    public static int ManhattanDist(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }
}
