using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 敌人 AI 效用评分系统（纯静态逻辑，零 Manager 依赖，可单测）。
/// 调用方（EnemyAI）负责收集战场快照（AiBattleData）传入；决策与执行分离——本类只产出计划（AiAction），不执行。
///
/// 全局目标优先级：攻击门 &gt; 击杀 &gt; 造成伤害 &gt; 靠近门 &gt; 躲避伤害 &gt; 逃跑（权重见常量表，数值保证链式自洽）。
/// 三档分流：简单=无脑冲（永不跳过，用光行动点）；标准=效用评分（火力区/刷怪格/卡位/送死检查/留点）；
/// 狡诈=标准 + 集火加成/弱侧门偷家/两步前瞻/预测威胁区（玩家移动后火力）/行动顺序决策。
/// </summary>
public static class AiTactics
{
    /// <summary>
    /// 决策日志输出钩子（默认 null 不输出；EnemyAI.DebugDecisions 勾选时订阅 GD.Print 便于观察决策过程）。
    /// AiTactics 保持零 Godot 依赖——日志走回调，测试不订阅即无输出。
    /// </summary>
    public static System.Action<string> DebugLog;

    private static void Log(string msg)
    {
        DebugLog?.Invoke(msg);
    }

    // ── 目标价值权重（优先级：攻击门 > 击杀 > 造成伤害 > 靠近门 > 躲避伤害 > 逃跑）──
    private const int DoorScore = 100000;      // 攻击门（胜负关键）
    private const int KillScore = 50000;       // 一击击杀（CurrentHP ≤ AttackPower）
    private const int DamagePerHp = 3000;      // 造成伤害（每点）
    private const int ThreatPerAtk = 400;      // 目标攻击力威胁（每点）
    private const int DistPerStep = 400;       // 目标距离惩罚（每步；移动评分中同值反向生效=靠近门收益）
    private const int FocusBonus = 4000;       // 集火加成（狡诈）

    // ── 移动格评分 ──
    // 靠近目标（门）权重 2026-08-09 提高：改为"相对当前格"的接近奖励——每接近 1 步 +3000（绝对正收益），
    // 不再用绝对距离惩罚（远处接近 1 步仍是负分会把前期弱单位钉死在原地/只能后退）。
    // 2026-08-09 再提高 2000→3000：推进/压门更主导（优先级：靠近门 > 躲避伤害 > 卡位）。
    private const int ApproachPerStep = 3000;        // 靠近目标每步价值（优先级：靠近门 > 躲避伤害）
    private const int FireZonePenalty = 6000;        // 玩家火力区（躲避伤害）
    private const int DodgeBonus = 6000;             // 逃离火力区奖励（当前格在火力区 → 候选格不在 → +6000；**不随稀有度缩放**——
                                                     //   能跑就跑是所有单位的基本生存行为，2026-08-09 修复"站在火力区能跑不跑站着挨打"；
                                                     //   而"进入火力区的代价"才按惜命系数缩放（炮灰敢进，惜命不进））
    private const int SpawnCellPenalty = 6000;       // 刷怪红格（避开援军刷新格）
    private const int PredictedThreatPenalty = 4000; // 预测威胁区（玩家移动后火力）
    private const int PredictedLethalPenalty = 30000;// 预测被击杀格（玩家移动后能一击反杀）
    // 卡位（2026-08-09 按策划语义重做：压制走位 + 贴身堵，替换旧的"站玩家移动可达格"）：
    // 2026-08-09 降权：压制 4000→2000、堵 3000→1500——推进/攻击更主导（卡位只是次要走位）。
    // 炮灰（初级/中级）完全忽略卡位奖励（直接往前冲，不停环上、不堵）。
    private const int PressureBonus = 2000;          // 压制走位：站"玩家攻击范围边缘外一格"（距玩家恰为 AttackDistance+1 的环）
                                                     //   ——下回合玩家想打必须先移动，浪费行动点（攻击不到/攻击完后的走位）。
                                                     //   条件式给分：仅在"到达压制位"时（当前不在环上）——环上横移无奖励，
                                                     //   否则敌人会永远卡在环上与远程单位对峙（2026-08-09 修复）
    private const int AdjacentBlockBonus = 1500;     // 贴身堵：逃不掉（当前在火力区）时站玩家身边 4 格，缩玩家移动范围；
                                                     //   堵格不比原地更危险（都在挨打）→ 豁免火力区惩罚
    private const int DoorLookaheadBonus = 15000;    // 两步前瞻：下回合够得着门
    private const int KillLookaheadBonus = 6000;     // 两步前瞻：下回合可击杀目标

    // ── 逃跑（低血量且无有价值行动）──
    private const int EscapePerStep = 600;     // 每步远离玩家
    private const float LowHpRatio = 0.3f;     // 低血量阈值（≤30% 最大生命）

    // ── 横移绕路（留点前兜底）──
    private const int LateralBonus = 500;      // 横移格效用（仅用于日志/排序；行为是"无更近格时侧向探索"）

    // ── 行动顺序（狡诈）──
    private const int OrderKillNowBonus = 100000; // 当前格能击杀/打门 → 先手
    private const int OrderPerAtk = 1000;         // 攻击力
    private const int OrderPerDistStep = 100;     // 距最近玩家门

    // ── 防走丢 ──
    private const int StandardMaxDetour = 5;      // 标准：绕远超过当前格+3 步的候选跳过
    private const int CunningMaxDetour = 8;       // 狡诈：允许更大绕远（绕后偷家路径）

    /// <summary>
    /// 稀有度惜命系数（2026-08-09，牺牲意愿分层）：
    /// 初级/中级=炮灰（火力区/预测惩罚 ×0.25、不做送死检查、永不逃跑）——敢穿火线冲门推进；
    /// 高级=1.2；顶级=1.5（惜命，避免无谓换血）。
    /// 2026-08-09 二次调整：规避伤害权重整体再降，中级也当炮灰（保活权重过高 → 标准/狡诈推进反而不如简单级无脑冲）。
    /// </summary>
    public static float RarityCareFactor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.初级:
            case Rarity.中级: return 0.25f;
            case Rarity.高级: return 0.8f;
            case Rarity.顶级: return 1f;
            default: return 0.25f;
        }
    }

    /// <summary>牺牲值得的单位（初级/中级炮灰）：不做送死检查、永不逃跑</summary>
    private static bool IsExpendable(Unit u)
    {
        return RarityCareFactor(u?.UnitData?.Rarity ?? Rarity.中级) <= 0.25f;
    }

    /// <summary>
    /// 敌方 AI 决策入口（纯逻辑）：返回单个动作的计划；无可行动作返回 null（含玩家全灭）。
    /// 内部按 data.Level 分流：简单 → DecideSimpleAction；标准/狡诈 → DecideTacticalAction。
    /// </summary>
    public static AiAction DecideAction(Unit enemy, AiBattleData data)
    {
        if (enemy == null || data == null || data.Map == null) return null;
        if (!enemy.IsAlive || enemy.IsDead || enemy.ActionPoints <= 0) return null;

        switch (data.Level)
        {
            case AiLevel.简单: return DecideSimpleAction(enemy, data);
            case AiLevel.狡诈: return DecideTacticalAction(enemy, data, cunning: true);
            default: return DecideTacticalAction(enemy, data, cunning: false);
        }
    }

    // ======================================================================
    // 简单：无脑冲向玩家单位并攻击，用光所有行动点（永不跳过）；BFS 绕障逼近（不会被障碍卡死）
    // ======================================================================
    private static AiAction DecideSimpleAction(Unit enemy, AiBattleData data)
    {
        var map = data.Map;
        Log($"[AI][决策] {enemy.UnitData?.UnitName}@{enemy.GridPos} AP={enemy.ActionPoints} 攻击={enemy.AttackPower} 体力={enemy.Stamina} 难度=简单");

        // 1. 攻击：范围内打分选目标（门 > 击杀 > 威胁 > 距离；无送死判断——无脑冲）
        var target = PickAttackTarget(GetAttackableUnits(enemy, enemy.GridPos, data), enemy);
        if (target != null)
        {
            int s = ScoreTarget(enemy, target);
            Log($"[AI][攻击] 选 {target.UnitData?.UnitName}@{target.GridPos} 分{s}");
            return new AiAction
            {
                Kind = AiActionKind.Attack, Target = target,
                Utility = s, AttackValue = s,
                Reason = $"攻击{target.UnitData?.UnitName}",
            };
        }

        // 2. 无法攻击 → 沿 BFS 最短路径朝最近玩家移动（绕障）；永不跳过（全负效用也走）
        var nearestPlayer = FindNearestPlayer(enemy.GridPos, data);
        if (nearestPlayer == null)
        {
            Log($"[AI][决策] 未找到玩家单位");
            return null;
        }

        var reachable = PathFinder.GetReachableCells(enemy.GridPos, enemy.Stamina, map);
        var distToGoal = PathFinder.GetDistanceFrom(nearestPlayer.GridPos, map, enemy.GridPos, Team.Enemy);
        var best = PickBestMoveCell(enemy.GridPos, reachable, null, distToGoal,
            null, null, previousPos: data.PreviousPos, fallbackGoal: nearestPlayer.GridPos);
        if (!best.HasValue)
        {
            Log($"[AI][移动] 无可达格（体力 {enemy.Stamina}）");
            return null;
        }

        Log($"[AI][移动] 逼近 {nearestPlayer.UnitData?.UnitName}@{nearestPlayer.GridPos} → {best.Value}");
        return new AiAction { Kind = AiActionKind.Move, MovePos = best.Value, Utility = 0, Reason = $"逼近{nearestPlayer.UnitData?.UnitName}" };
    }

    // ======================================================================
    // 标准/狡诈：效用评分决策
    // ======================================================================
    private static AiAction DecideTacticalAction(Unit enemy, AiBattleData data, bool cunning)
    {
        var map = data.Map;
        if (data.PlayerUnits == null || data.PlayerUnits.Count == 0) return null;   // 无玩家 → 战斗应已结束

        var threatCells = ComputeThreatCells(map, data.PlayerUnits);            // 玩家火力区（不含门）
        var pressureCells = ComputePressureCells(map, data.PlayerUnits);        // 压制位：玩家攻击范围边缘外一格
        var adjacentCells = ComputeAdjacentCells(map, data.PlayerUnits);        // 玩家身边 4 格（贴身堵）
        var spawnCells = data.SpawnCells;                                  // 下回合刷怪格（标准+）
        HashSet<Vector2I> predictedThreat = null, predictedLethal = null;
        if (cunning)
        {
            predictedThreat = ComputePredictedThreatCells(map, data.PlayerUnits, lethalOnly: false, enemyHP: 0);
            predictedLethal = ComputePredictedThreatCells(map, data.PlayerUnits, lethalOnly: true, enemyHP: enemy.CurrentHP);
        }
        int maxDetour = cunning ? CunningMaxDetour : StandardMaxDetour;
        // 稀有度惜命系数：初级炮灰敢冲（惩罚 ×0.25/不做送死检查/不逃跑），高级+惜命（惩罚放大）
        float careFactor = RarityCareFactor(enemy.UnitData?.Rarity ?? Rarity.中级);

        var attackableNow = GetAttackableUnits(enemy, enemy.GridPos, data);
        var goal = cunning ? PickWeakestDoor(data, enemy.GridPos) : PickAttackTarget(data.PlayerUnits, enemy);
        if (goal == null) goal = FindNearestPlayer(enemy.GridPos, data);

        // 群体冲锋稀释：威胁区内/能进入的敌人数越多，被大炮秒的风险越低 → 威胁惩罚稀释（人多不怕）
        float threatDilution = CrowdDilution(data, threatCells);
        if (threatDilution < 1f)
            Log($"[AI][战场] 群体冲锋：{data.EnemyUnits.Count} 只敌人中 {Mathf.RoundToInt(1f / threatDilution)} 只可进入威胁区 → 威胁惩罚 ×{threatDilution:0.##}（人多不怕）");

        Log($"[AI][决策] {enemy.UnitData?.UnitName}@{enemy.GridPos} AP={enemy.ActionPoints} 攻击={enemy.AttackPower} 射程={enemy.AttackDistance} 体力={enemy.Stamina} HP={enemy.CurrentHP}/{enemy.MaxHP} 稀有度={enemy.UnitData?.Rarity} 难度={(cunning ? "狡诈" : "标准")} 惜命系数={careFactor}");
        Log($"[AI][战场] 玩家{data.PlayerUnits.Count}个(门{data.PlayerDoors.Count}) 目标={goal?.UnitData?.UnitName}@{goal?.GridPos} 火力区={threatCells.Count}格 压制位={pressureCells.Count}格 贴身位={adjacentCells.Count}格 刷怪格={spawnCells?.Count ?? 0}格{(cunning ? $" 预测威胁={predictedThreat.Count}格 预测被击杀={predictedLethal.Count}格" : "")}");
        if (attackableNow.Count > 0)
            Log($"[AI][目标] 当前可攻击: {string.Join(" | ", attackableNow.Select(t => $"{t.UnitData?.UnitName}@{t.GridPos} 分{ScoreTarget(enemy, t)}{(t.Type == UnitType.门 ? "(门)" : t.CurrentHP <= enemy.AttackPower ? "(可杀)" : "")}"))}");

        // 1. 攻击选项（打分 + 送死剔除[初级豁免] + 狡诈集火加成）——攻击优先于让位（2026-08-09：能杀则杀，不能杀再让）
        var attackOption = PickBestAttack(enemy, attackableNow, data, cunning, careFactor);

        // 1.5 让位：站在下回合刷怪格且本回合**无法攻击**（攻击无效）→ 先移开让位。
        //    攻击有效则直接打（含击杀门——被攻击优先天然覆盖，旧的 killDoorNow 豁免删除）；
        //    让位移动含移动+攻击连招价值（AP≥2 移动出红格再打）
        if ((attackOption == null || attackOption.Utility <= 0)
            && spawnCells != null && spawnCells.Contains(enemy.GridPos))
        {
            var yieldPlan = BuildMoveOption(enemy, data, goal, threatCells, spawnCells,
                predictedThreat, predictedLethal, pressureCells, adjacentCells, maxDetour,
                excludeSpawnCells: true, skipIfNoValue: false, careFactor: careFactor, threatDilution: threatDilution);
            if (yieldPlan != null)
            {
                yieldPlan.Reason = "让位（刷怪格）";
                Log($"[AI][决策] 攻击无效 + 站在刷怪红格 → 让位 {yieldPlan.MovePos}");
                return yieldPlan;
            }
            // 无可离开的格 → 继续正常决策
        }

        // 2. 移动选项（移动+攻击连招仅 AP≥2；两步前瞻仅狡诈且当前打不到时计入——保证"造成伤害 > 靠近门"）。
        //    炮灰（初级/中级）skipIfNoValue=false：任何候选都走，永不留点——强制用光所有 AP 往前冲
        bool expendable = IsExpendable(enemy);
        if (expendable) Log($"[AI][决策] 炮灰单位：强制用光 AP（移动永不跳过，忽略卡位奖励）");
        var moveOption = BuildMoveOption(enemy, data, goal, threatCells, spawnCells,
            predictedThreat, predictedLethal, pressureCells, adjacentCells, maxDetour,
            excludeSpawnCells: false, skipIfNoValue: !expendable, canLookahead: cunning && attackOption == null,
            careFactor: careFactor, threatDilution: threatDilution);

        // 3. 效用对比：更好攻击位（移动+攻击连招）> 当前攻击 > 有价值移动 > 逃跑 > 留点。
        //    炮灰（expendable）：负效用移动也执行（强制用光 AP 往前冲，永不 Skip）
        if (attackOption != null)
        {
            if (moveOption != null && moveOption.AttackValue > attackOption.Utility)
            {
                Log($"[AI][决策] 移动+攻击连招胜出（移动位攻击价值 {moveOption.AttackValue} > 当前攻击 {attackOption.Utility}）");
                return moveOption;
            }
            if (attackOption.Utility > 0)
            {
                Log($"[AI][决策] 攻击 {attackOption.Target.UnitData?.UnitName}@{attackOption.Target.GridPos} 总分 {attackOption.Utility}");
                return attackOption;
            }
        }
        if (moveOption != null && (moveOption.Utility > 0 || expendable))
        {
            if (moveOption.Utility <= 0)
                Log($"[AI][决策] 炮灰：负效用移动（{moveOption.Utility}）也执行——强制用光 AP");
            return moveOption;
        }

        // 4. 逃跑：低血量且无有价值行动（躲避伤害 > 逃跑：能做的事都做完了才跑）
        if (IsLowHp(enemy, data))
        {
            var esc = PickEscapeCell(enemy.GridPos,
                PathFinder.GetReachableCells(enemy.GridPos, enemy.Stamina, map),
                threatCells, spawnCells, data.PlayerUnits, data.PreviousPos);
            if (esc.HasValue)
            {
                Log($"[AI][决策] 低血({enemy.CurrentHP}/{enemy.MaxHP})无路可进 → 逃跑 {esc.Value}");
                return new AiAction { Kind = AiActionKind.Move, MovePos = esc.Value, Utility = EscapePerStep, Reason = "逃跑" };
            }
        }

        // 5. 横移绕路：无更近格（目标被玩家挡路/墙挡/已到最近格但够不到）时向侧向探索，
        //    避免"无威胁满行动点却站着不动"（2026-08-09 修复）；只在留点前兜底，不进任何惩罚区
        var lateral = PickLateralCell(enemy, data, goal, threatCells, spawnCells, predictedThreat, predictedLethal);
        if (lateral.HasValue)
        {
            Log($"[AI][决策] 无更近格 → 横移绕路 {lateral.Value}");
            return new AiAction { Kind = AiActionKind.Move, MovePos = lateral.Value, Utility = LateralBonus, Reason = "横移绕路" };
        }

        Log($"[AI][决策] 无有价值行动 → 留行动点");
        return new AiAction { Kind = AiActionKind.Skip, Utility = 0, Reason = "留行动点" };
    }

    /// <summary>攻击选项：打分选目标（门 > 击杀 > 伤害威胁 > 距离），剔除送死攻击（打不死+会被反杀；**初级炮灰豁免**——牺牲值得），狡诈叠加集火加成</summary>
    private static AiAction PickBestAttack(Unit enemy, List<Unit> attackable, AiBattleData data, bool cunning, float careFactor)
    {
        if (attackable == null || attackable.Count == 0) return null;

        Unit best = null;
        int bestScore = int.MinValue;
        foreach (var t in attackable)
        {
            int s = ScoreTarget(enemy, t);
            if (cunning && data.FocusTarget != null && t == data.FocusTarget && t.Type != UnitType.门)
            {
                s += FocusBonus;
                Log($"[AI][目标] {t.UnitData?.UnitName} 集火目标 +{FocusBonus} → 分{s}");
            }
            if (careFactor >= 0.5f && IsSuicidalAttack(enemy, t, data.PlayerUnits, data.Map))
            {
                Log($"[AI][目标] 剔除送死: {t.UnitData?.UnitName}@{t.GridPos}（打不死会被反杀）");
                continue;   // 白送不打（门/一击击杀在内部豁免）；初级炮灰不做送死检查
            }
            if (s > bestScore) { bestScore = s; best = t; }
        }
        if (best == null)
        {
            if (attackable.Count > 0) Log($"[AI][攻击] 全部目标送死剔除 → 不攻击");
            return null;
        }

        string suffix = best.Type == UnitType.门 ? "（门）" : best.CurrentHP <= enemy.AttackPower ? "（击杀）" : "";
        Log($"[AI][攻击] 选 {best.UnitData?.UnitName}@{best.GridPos} 分{bestScore}{suffix}");
        return new AiAction
        {
            Kind = AiActionKind.Attack, Target = best,
            Utility = bestScore, AttackValue = bestScore,
            Reason = $"攻击{best.UnitData?.UnitName}{suffix}",
        };
    }

    /// <summary>
    /// 移动选项：对可达格做效用评分选最优落点。
    /// 评分 = 移动后攻击价值（仅 AP≥2 计入） + 两步前瞻（狡诈） + 卡位 + 接近奖励 − 火力区×careFactor − 刷怪格 − 预测威胁×careFactor − 预测被击杀×careFactor。
    /// excludeSpawnCells=true（让位）：完全排除刷怪格且不应用跳过阈值（强制移开）。
    /// </summary>
    private static AiAction BuildMoveOption(Unit enemy, AiBattleData data, Unit goal,
        HashSet<Vector2I> threatCells, HashSet<Vector2I> spawnCells,
        HashSet<Vector2I> predictedThreat, HashSet<Vector2I> predictedLethal,
        HashSet<Vector2I> pressureCells, HashSet<Vector2I> adjacentCells, int maxDetour,
        bool excludeSpawnCells, bool skipIfNoValue, bool canLookahead = false, float careFactor = 0.75f,
        float threatDilution = 1f)
    {
        var map = data.Map;
        var reachable = PathFinder.GetReachableCells(enemy.GridPos, enemy.Stamina, map);
        if (reachable.Count == 0) return null;

        var attackScoreAtCell = BuildAttackScores(enemy, reachable, data, cunning: predictedThreat != null, careFactor: careFactor);
        var distToGoal = PathFinder.GetDistanceFrom(goal.GridPos, map, enemy.GridPos, Team.Enemy);
        bool goalReachable = distToGoal.ContainsKey(enemy.GridPos);
        int curDist = goalReachable ? distToGoal[enemy.GridPos] : ManhattanDist(enemy.GridPos, goal.GridPos);

        // 两步前瞻（狡诈且当前打不到）：该格下回合够得着门/击杀目标 → 折扣加成
        Dictionary<Vector2I, int> lookahead = null;
        if (canLookahead)
        {
            lookahead = new Dictionary<Vector2I, int>();
            foreach (var pos in reachable)
            {
                int v = NextTurnValue(enemy, pos, data);
                if (v > 0) lookahead[pos] = v;
            }
        }

        // 是否存在比当前格更接近目标的候选格（推进优先：存在则逃离奖励不生效，防止进火线又退回来）
        bool canApproach = false;
        foreach (var pos in reachable)
        {
            if (pos == enemy.GridPos) continue;
            int d = distToGoal.TryGetValue(pos, out int dv) ? dv : ManhattanDist(pos, goal.GridPos);
            if (d < curDist) { canApproach = true; break; }
        }

        Vector2I? bestPos = null;
        int bestScore = int.MinValue;
        Log($"[AI][移动] 目标={goal?.UnitData?.UnitName}@{goal?.GridPos} 可达{reachable.Count}格 距离{curDist}步 {(canApproach ? "可推进" : "无更近格")}");
        foreach (var pos in reachable)
        {
            if (pos == enemy.GridPos) continue;
            if (excludeSpawnCells && spawnCells != null && spawnCells.Contains(pos)) continue;
            if (data.PreviousPos.HasValue && pos == data.PreviousPos.Value) continue;   // 防来回动
            int dist = distToGoal.TryGetValue(pos, out int d) ? d : ManhattanDist(pos, goal.GridPos);
            if (goalReachable && dist > curDist + maxDetour) continue;                   // 防走丢：绕远过猛跳过
            int s = ScoreMoveCell(pos, enemy.GridPos, attackScoreAtCell, distToGoal,
                threatCells, spawnCells, predictedThreat, predictedLethal, pressureCells, adjacentCells, lookahead,
                fallbackGoal: goal.GridPos, careFactor: careFactor, canApproach: canApproach, threatDilution: threatDilution);
            if (DebugLog != null)
            {
                // 评分分解（仅显示非零项；炮灰忽略卡位奖励，分解与实算一致）
                bool isCannon = careFactor <= 0.25f;
                var parts = new System.Collections.Generic.List<string>();
                if (attackScoreAtCell != null && attackScoreAtCell.TryGetValue(pos, out int av) && av != 0)
                    parts.Add($"攻击+{av}");
                int appr = (curDist - dist) * ApproachPerStep;
                if (appr != 0) parts.Add($"接近{appr:+#;-#;0}");
                if (!isCannon && pressureCells != null && !pressureCells.Contains(enemy.GridPos) && pressureCells.Contains(pos))
                    parts.Add($"压制+{PressureBonus}");
                if (threatCells != null && threatCells.Contains(enemy.GridPos) && !threatCells.Contains(pos) && !canApproach)
                    parts.Add($"逃离+{DodgeBonus}");
                if (!isCannon && adjacentCells != null && adjacentCells.Contains(pos) && threatCells != null && threatCells.Contains(enemy.GridPos))
                    parts.Add($"堵+{AdjacentBlockBonus}");
                if (threatCells != null && threatCells.Contains(pos))
                    parts.Add($"火力-{(int)(FireZonePenalty * careFactor * threatDilution)}");
                if (spawnCells != null && spawnCells.Contains(pos)) parts.Add($"刷怪-{SpawnCellPenalty}");
                if (predictedThreat != null && predictedThreat.Contains(pos)) parts.Add($"预测威胁-{(int)(PredictedThreatPenalty * careFactor)}");
                if (!isCannon && predictedLethal != null && predictedLethal.Contains(pos)) parts.Add($"预测被击杀-{(int)(PredictedLethalPenalty * careFactor * threatDilution)}");
                if (lookahead != null && lookahead.TryGetValue(pos, out int lv)) parts.Add($"前瞻+{lv}");
                Log($"[AI][移动]   {pos}: 总分{s} [{string.Join(" ", parts)}]");
            }
            if (s > bestScore) { bestScore = s; bestPos = pos; }
        }
        if (!bestPos.HasValue) { Log($"[AI][移动] 无可用候选格"); return null; }
        if (skipIfNoValue && bestScore <= 0) { Log($"[AI][移动] 最优候选 {bestPos} 总分 {bestScore} ≤0 → 无收益移动"); return null; }

        int atkVal = 0;
        if (attackScoreAtCell != null) attackScoreAtCell.TryGetValue(bestPos.Value, out atkVal);
        string reason = BuildMoveReason(enemy, bestPos.Value, atkVal, goal, distToGoal,
            pressureCells, adjacentCells, threatCells, lookahead);
        Log($"[AI][移动] 选 {bestPos} 总分 {bestScore}（{reason}）");
        return new AiAction
        {
            Kind = AiActionKind.Move, MovePos = bestPos.Value,
            Utility = bestScore, AttackValue = atkVal,
            Reason = reason,
        };
    }

    private static string BuildMoveReason(Unit enemy, Vector2I pos, int atkVal, Unit goal,
        IReadOnlyDictionary<Vector2I, int> distToGoal, IReadOnlySet<Vector2I> pressureCells,
        IReadOnlySet<Vector2I> adjacentCells, IReadOnlySet<Vector2I> threatCells,
        IReadOnlyDictionary<Vector2I, int> lookahead)
    {
        if (atkVal > 0) return $"移动进攻位（价值 {atkVal}）";
        if (goal != null && distToGoal != null
            && distToGoal.TryGetValue(pos, out int d) && distToGoal.TryGetValue(enemy.GridPos, out int cd) && d < cd)
            return $"逼近{goal.UnitData?.UnitName}";
        if (pressureCells != null && pressureCells.Contains(pos)) return "压制走位";
        if (adjacentCells != null && adjacentCells.Contains(pos)
            && threatCells != null && threatCells.Contains(enemy.GridPos)) return "贴身卡位";
        if (lookahead != null && lookahead.ContainsKey(pos)) return "两步前瞻";
        return $"移动至 ({pos.X},{pos.Y})";
    }

    /// <summary>
    /// 计算每格"移动后可攻击目标"的最高得分（标准+ 连招用）。
    /// 仅 AP≥2 时计入——AP=1 移动即耗光行动点，移动后攻击价值是假的。
    /// 含送死剔除（落点会被反杀；初级炮灰豁免）与狡诈集火加成。
    /// </summary>
    private static Dictionary<Vector2I, int> BuildAttackScores(Unit enemy, HashSet<Vector2I> reachable, AiBattleData data, bool cunning, float careFactor)
    {
        var scores = new Dictionary<Vector2I, int>();
        if (enemy.ActionPoints < 2) return scores;

        foreach (var pos in reachable)
        {
            int bestScore = int.MinValue;
            foreach (var t in GetAttackableUnits(enemy, pos, data))
            {
                // 距离按候选格算（不传则 ScoreTarget 用攻击者当前格距离，会把远处攻击位与近处攻击位打平）
                int s = ScoreTarget(enemy, t, ManhattanDist(pos, t.GridPos));
                if (cunning && data.FocusTarget != null && t == data.FocusTarget && t.Type != UnitType.门)
                    s += FocusBonus;
                if (s > int.MinValue && careFactor >= 0.5f && IsSuicidalAttack(enemy, t, data.PlayerUnits, data.Map, fromPos: pos))
                    continue;   // 送死连招剔除（落点会被反杀）；初级炮灰豁免
                if (s > bestScore) bestScore = s;
            }
            if (bestScore > int.MinValue) scores[pos] = bestScore;
        }
        return scores;
    }

    // ======================================================================
    // 目标价值打分（签名保留，兼容旧测试；行为按新权重）
    // ======================================================================
    /// <summary>
    /// 目标价值打分（优先级：攻击门 &gt; 击杀 &gt; 造成伤害；同价值近距离优先）：
    /// 门 +100000；一击可杀 +50000；其他 = 伤害每点 3000 + 目标攻击力威胁每点 400；统一 − 距离×400。
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
            score += KillScore;
        score += DamagePerHp * Mathf.Min(attacker.AttackPower, Mathf.Max(target.CurrentHP, 0)); // 造成伤害
        score += target.AttackPower * ThreatPerAtk;                                             // 目标威胁
        score -= distance * DistPerStep;                                                        // 距离惩罚
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
    /// 门除外（胜负关键，送死值得）。攻击不改变位置，默认按 attacker 当前格判断反杀覆盖；
    /// fromPos 可选：移动+攻击连招的落点反杀检查（连招后站在新位置，覆盖玩家火力不同）。
    /// </summary>
    /// <param name="playerUnits">全部存活玩家单位（含门）</param>
    public static bool IsSuicidalAttack(Unit attacker, Unit target,
        IEnumerable<Unit> playerUnits, Dictionary<Vector2I, Cell> map, Vector2I? fromPos = null)
    {
        if (attacker == null || target == null) return false;
        if (attacker.AttackPower >= target.CurrentHP) return false;   // 一击击杀 → 不亏
        if (target.Type == UnitType.门) return false;                  // 打门值得（胜负关键）

        Vector2I attackPos = fromPos ?? attacker.GridPos;
        foreach (var u in playerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            if (u.Team == attacker.Team) continue;
            if (u.Type == UnitType.门) continue;   // 门不算反杀者：打门旁单位挨门打是预期成本（否则弱敌人被门火力判送死而退缩）
            if (u.AttackPower < attacker.CurrentHP) continue;          // 一击杀不死 attacker
            if (map == null) continue;
            map.TryGetValue(u.GridPos, out Cell cell);
            var ctx = new Context { SourceUnit = u, Map = map, TargetCell = cell };
            if (PathFinder.GetAttackableTargets(u.GridPos, u.AttackShape, u.AttackDistance, u.Team, map, ctx)
                .Contains(attackPos))
                return true;
        }
        return false;
    }

    // ======================================================================
    // 移动格评分（PickBestMoveCell 兼容旧签名；ScoreMoveCell 为逐格评分纯函数）
    // ======================================================================
    /// <summary>
    /// 移动格评分选择（纯函数）：返回评分最高的移动目标格；无可选格返回 null（跳过行动）。
    /// 评分 = 移动后攻击价值（attackScoreAtCell）
    ///       + 两步前瞻（lookaheadBonus） + 压制走位（pressureCells +4000）/ 贴身堵（adjacentCells +3000，逃不掉时）
    ///       + 靠近目标奖励（curDist−dist）×ApproachPerStep（绕障 BFS 优先；不可达回退曼哈顿 fallbackGoal）
    ///       − 火力区 − 刷怪格 − 预测威胁区 − 预测被击杀格
    /// 靠近目标奖励是"相对当前格"的绝对正收益（每接近 1 步 +2000）——远处接近也走，
    /// 不会因绝对距离惩罚把单位钉死在原地（2026-08-09 修复前期弱单位只会后退）。
    /// 防"来回动"三件套：previousPos 禁止直接回头；maxDetour 绕远上限；候选格必须存在才返回。
    /// </summary>
    /// <param name="currentPos">单位当前格（不会选中自身）</param>
    /// <param name="reachable">可达格集合</param>
    /// <param name="attackScoreAtCell">每格"移动后可攻击目标"的最高得分（null/不含=不能攻击）</param>
    /// <param name="distToGoal">每格到进攻目标的绕障成本（不含=不可达）</param>
    /// <param name="threatenedCells">玩家攻击威胁格集（null=不规避；**不含门**——门是攻击目标不是威胁源）</param>
    /// <param name="spawnCells">下回合刷怪格集（null=不回避）</param>
    /// <param name="excludeSpawnCells">true=完全排除刷怪格（主动让位），false=仅惩罚</param>
    /// <param name="previousPos">上回合移动起点（禁止移回该格，防 A↔B 来回）</param>
    /// <param name="maxDetour">允许绕远的步数上限（候选 dist 不得超过当前格 dist + 该值，防无限走丢）</param>
    /// <param name="fallbackGoal">绕障不可达时的曼哈顿回退目标（通常=进攻目标格；null=不可达则跳过该格）</param>
    /// <param name="extraScoreAtCell">附加按格评分（两步前瞻/卡位等；null=无）</param>
    public static Vector2I? PickBestMoveCell(
        Vector2I currentPos,
        IReadOnlyCollection<Vector2I> reachable,
        IReadOnlyDictionary<Vector2I, int> attackScoreAtCell,
        IReadOnlyDictionary<Vector2I, int> distToGoal,
        IReadOnlySet<Vector2I> threatenedCells,
        IReadOnlySet<Vector2I> spawnCells,
        bool excludeSpawnCells = false,
        Vector2I? previousPos = null,
        int maxDetour = 3,
        Vector2I? fallbackGoal = null,
        IReadOnlyDictionary<Vector2I, int> extraScoreAtCell = null)
    {
        if (reachable == null || distToGoal == null) return null;

        int curDist = distToGoal.TryGetValue(currentPos, out int c)
            ? c : (fallbackGoal.HasValue ? ManhattanDist(currentPos, fallbackGoal.Value) : -1);
        if (curDist < 0) return null;   // 无距离信息且无回退目标 → 跳过

        Vector2I? best = null;
        int bestScore = int.MinValue;

        foreach (var pos in reachable)
        {
            if (pos == currentPos) continue;
            if (excludeSpawnCells && spawnCells != null && spawnCells.Contains(pos)) continue;
            if (previousPos.HasValue && pos == previousPos.Value) continue;   // 禁止直接回头
            int dist = distToGoal.TryGetValue(pos, out int d)
                ? d : (fallbackGoal.HasValue ? ManhattanDist(pos, fallbackGoal.Value) : -1);
            if (dist < 0) continue;
            if (dist > curDist + maxDetour) continue;                   // 绕远过猛 → 防走丢

            int s = ScoreMoveCell(pos, currentPos, attackScoreAtCell, distToGoal,
                threatenedCells, spawnCells, null, null, null, null, extraScoreAtCell, fallbackGoal);
            if (s > bestScore)
            {
                bestScore = s;
                best = pos;
            }
        }

        return best;
    }

    /// <summary>逐格移动效用评分（纯函数，供 PickBestMoveCell 与测试断言）：见 PickBestMoveCell 文档。距离信息缺失返回 int.MinValue（不可评估）。
    /// careFactor = 稀有度惜命系数（RarityCareFactor）：火力区/预测威胁/预测被击杀惩罚按此缩放——初级炮灰 0.25 敢穿火线，顶级 1.5 惜命。
    /// pressureCells = 压制位（玩家攻击范围边缘外一格）；adjacentCells = 玩家身边 4 格（贴身堵）；
    /// canApproach = 是否存在比当前格更接近目标的候选格（有则推进优先，逃离奖励不生效）。</summary>
    public static int ScoreMoveCell(
        Vector2I pos,
        Vector2I currentPos,
        IReadOnlyDictionary<Vector2I, int> attackScoreAtCell,
        IReadOnlyDictionary<Vector2I, int> distToGoal,
        IReadOnlySet<Vector2I> threatenedCells,
        IReadOnlySet<Vector2I> spawnCells,
        IReadOnlySet<Vector2I> predictedThreat,
        IReadOnlySet<Vector2I> predictedLethal,
        IReadOnlySet<Vector2I> pressureCells,
        IReadOnlySet<Vector2I> adjacentCells,
        IReadOnlyDictionary<Vector2I, int> lookaheadBonus,
        Vector2I? fallbackGoal = null,
        float careFactor = 1f,
        bool canApproach = false,
        float threatDilution = 1f)
    {
        int score = 0;
        // 炮灰（初级/中级）：完全忽略卡位奖励（压制/堵）——强制往前冲，不停环上、不堵（2026-08-09）
        bool isCannon = careFactor <= 0.25f;
        if (attackScoreAtCell != null && attackScoreAtCell.TryGetValue(pos, out int atk))
            score += atk;
        if (lookaheadBonus != null && lookaheadBonus.TryGetValue(pos, out int lk))
            score += lk;

        // 压制走位：到达"玩家攻击范围边缘外一格"（当前不在压制位、候选在）→ 逼玩家浪费行动点；
        // 条件式给分：已在压制位上横移无奖励，否则敌人永远卡在环上与远程单位对峙
        if (!isCannon && pressureCells != null && !pressureCells.Contains(currentPos) && pressureCells.Contains(pos))
            score += PressureBonus;

        // 贴近目标奖励（相对当前格）：每接近 1 步 +2000，绝对正收益（远离=负）
        int curDist = distToGoal != null && distToGoal.TryGetValue(currentPos, out int cd)
            ? cd : (fallbackGoal.HasValue ? ManhattanDist(currentPos, fallbackGoal.Value) : -1);
        int dist = distToGoal != null && distToGoal.TryGetValue(pos, out int d)
            ? d : (fallbackGoal.HasValue ? ManhattanDist(pos, fallbackGoal.Value) : -1);
        if (curDist < 0 || dist < 0) return int.MinValue;
        score += (curDist - dist) * ApproachPerStep;

        // 逃离火力区奖励：当前站在玩家攻击范围内 → 候选格不在 → 能跑就跑（不站着挨打）。
        // 仅"无更近格可推进"时生效（canApproach=false）——否则敌人一走进火力区就被拉回，
        // 永远无法推进攻击远程单位（2026-08-09 修复：推进优先于逃离）
        bool inFireNow = threatenedCells != null && threatenedCells.Contains(currentPos);
        if (inFireNow && !threatenedCells.Contains(pos) && !canApproach)
            score += DodgeBonus;

        // 贴身堵：逃不掉（当前在火力区）→ 站玩家身边 4 格，缩玩家移动范围；
        // 堵格不比原地更危险（都在挨打）→ 豁免火力区惩罚
        bool adjacentBlock = adjacentCells != null && adjacentCells.Contains(pos);
        if (!isCannon && inFireNow && adjacentBlock)
            score += AdjacentBlockBonus;

        // 火力区/预测被击杀惩罚按群体稀释（threatDilution = 1/N，人多不怕大炮秒杀）
        if (threatenedCells != null && threatenedCells.Contains(pos) && !(inFireNow && adjacentBlock))
            score -= (int)(FireZonePenalty * careFactor * threatDilution);
        if (spawnCells != null && spawnCells.Contains(pos))
            score -= SpawnCellPenalty;   // 刷怪惩罚不缩放（战术行为，与保命无关）
        if (predictedThreat != null && predictedThreat.Contains(pos))
            score -= (int)(PredictedThreatPenalty * careFactor);
        // 预测被击杀惩罚：炮灰（初级/中级）无视——不怕死，用命换推进（2026-08-09）
        if (!isCannon && predictedLethal != null && predictedLethal.Contains(pos))
            score -= (int)(PredictedLethalPenalty * careFactor * threatDilution);
        return score;
    }

    /// <summary>
    /// 群体冲锋稀释（2026-08-09）：统计"当前处于或体力内可进入威胁区"的敌方单位数 N。
    /// 大炮/秒杀类单位一回合只能处理一个目标——人多一起冲，每只的实际死亡风险 ≈ 1/N，
    /// 威胁惩罚（火力区/预测被击杀）按此稀释。N=0/1 不稀释（单只独自面对大炮 → 该躲就躲）。
    /// </summary>
    /// <param name="threatZone">威胁区（火力区并集；调用方也可传预测被击杀区）</param>
    public static float CrowdDilution(AiBattleData data, IReadOnlySet<Vector2I> threatZone)
    {
        if (data?.EnemyUnits == null || threatZone == null || threatZone.Count == 0) return 1f;
        int count = 0;
        foreach (var e in data.EnemyUnits)
        {
            if (e == null || !e.IsAlive || e.IsDead) continue;
            if (threatZone.Contains(e.GridPos)) { count++; continue; }   // 已在威胁区内
            foreach (var pos in PathFinder.GetReachableCells(e.GridPos, e.Stamina, data.Map))
            {
                if (threatZone.Contains(pos)) { count++; break; }        // 体力内能走进
            }
        }
        return count >= 2 ? 1f / count : 1f;
    }

    // ======================================================================
    // 逃跑 / 两步前瞻 / 行动顺序 / 弱侧门
    // ======================================================================
    /// <summary>
    /// 逃跑落点：从可达格中选"远离玩家"价值最高的格（每步 +600，封顶 8 步），火力区/刷怪格重罚。
    /// 无可逃格返回 null。
    /// </summary>
    public static Vector2I? PickEscapeCell(Vector2I currentPos, IReadOnlyCollection<Vector2I> reachable,
        IReadOnlySet<Vector2I> threatenedCells, IReadOnlySet<Vector2I> spawnCells,
        IReadOnlyCollection<Unit> playerUnits, Vector2I? previousPos = null)
    {
        if (reachable == null) return null;

        Vector2I? best = null;
        int bestScore = int.MinValue;
        foreach (var pos in reachable)
        {
            if (pos == currentPos) continue;
            if (previousPos.HasValue && pos == previousPos.Value) continue;
            int score = EscapePerStep * Mathf.Min(DistToNearestPlayer(pos, playerUnits), 8);
            if (threatenedCells != null && threatenedCells.Contains(pos)) score -= FireZonePenalty;
            if (spawnCells != null && spawnCells.Contains(pos)) score -= SpawnCellPenalty;
            if (score > bestScore) { bestScore = score; best = pos; }
        }
        return best;
    }

    /// <summary>
    /// 横移绕路兜底：无更近格时，从可达格中找"距离不变（横移）、不在任何惩罚区、不回头"的格（侧向探索绕开挡路）。
    /// 无目标（goal null）或无可选格返回 null（留点）。
    /// </summary>
    private static Vector2I? PickLateralCell(Unit enemy, AiBattleData data, Unit goal,
        IReadOnlySet<Vector2I> threatCells, IReadOnlySet<Vector2I> spawnCells,
        IReadOnlySet<Vector2I> predictedThreat, IReadOnlySet<Vector2I> predictedLethal)
    {
        if (goal == null || data?.Map == null) return null;

        var map = data.Map;
        var reachable = PathFinder.GetReachableCells(enemy.GridPos, enemy.Stamina, map);
        if (reachable.Count == 0) return null;

        var distToGoal = PathFinder.GetDistanceFrom(goal.GridPos, map, enemy.GridPos, Team.Enemy);
        int curDist = distToGoal.TryGetValue(enemy.GridPos, out int cd)
            ? cd : ManhattanDist(enemy.GridPos, goal.GridPos);

        foreach (var pos in reachable)
        {
            if (pos == enemy.GridPos) continue;
            if (data.PreviousPos.HasValue && pos == data.PreviousPos.Value) continue;   // 不回头
            int dist = distToGoal.TryGetValue(pos, out int d) ? d : ManhattanDist(pos, goal.GridPos);
            if (dist != curDist) continue;   // 只横移：更近已在主评分处理，更远=倒退
            if (threatCells != null && threatCells.Contains(pos)) continue;
            if (spawnCells != null && spawnCells.Contains(pos)) continue;
            if (predictedThreat != null && predictedThreat.Contains(pos)) continue;
            if (predictedLethal != null && predictedLethal.Contains(pos)) continue;
            return pos;   // 第一个安全横移格
        }
        return null;
    }

    /// <summary>
    /// 两步前瞻（狡诈）：从 pos 出发，下回合（满 AP 再移动一次）能否够到门/可击杀目标。
    /// **绕障距离**（GetDistanceFrom BFS，墙/玩家占据格真实挡路；队友格可穿越；自身格忽略）
    /// 而非曼哈顿——2026-08-09 修复：曼哈顿会穿墙误判"够得到"。
    /// 够着 = 绕障成本 ≤ Stamina+AttackDistance。返回加成值：门 15000 / 击杀 6000，取高。
    /// </summary>
    public static int NextTurnValue(Unit enemy, Vector2I pos, AiBattleData data)
    {
        if (enemy == null || data == null || data.Map == null) return 0;
        int range = enemy.Stamina + enemy.AttackDistance;

        int v = 0;
        if (data.PlayerDoors != null)
        {
            foreach (var door in data.PlayerDoors)
            {
                if (door == null || !door.IsAlive || door.IsDead) continue;
                var dist = PathFinder.GetDistanceFrom(door.GridPos, data.Map, enemy.GridPos, Team.Enemy);
                if (dist.TryGetValue(pos, out int d) && d <= range)
                {
                    v = Mathf.Max(v, DoorLookaheadBonus);
                    break;
                }
            }
        }
        if (data.PlayerUnits != null)
        {
            foreach (var t in data.PlayerUnits)
            {
                if (t == null || !t.IsAlive || t.IsDead || t.Type == UnitType.门) continue;
                if (t.CurrentHP > enemy.AttackPower) continue;   // 打不死 → 无击杀前瞻
                var dist = PathFinder.GetDistanceFrom(t.GridPos, data.Map, enemy.GridPos, Team.Enemy);
                if (dist.TryGetValue(pos, out int d) && d <= range)
                {
                    v = Mathf.Max(v, KillLookaheadBonus);
                    break;
                }
            }
        }
        return v;
    }

    /// <summary>
    /// 狡诈行动顺序粗效用：当前格能击杀/打门 +100000，+1000/攻击力点，−100/距最近玩家门步。
    /// 降序排序 = 杀手/高攻/近门先手（EnemyAI.StartAITurn 用）。
    /// </summary>
    public static int RoughPriority(Unit enemy, AiBattleData data)
    {
        if (enemy == null || !enemy.IsAlive || enemy.IsDead) return int.MinValue;

        int score = enemy.AttackPower * OrderPerAtk;
        if (data.PlayerDoors != null && data.PlayerDoors.Count > 0)
        {
            int minDist = int.MaxValue;
            foreach (var d in data.PlayerDoors)
                if (d != null) minDist = Mathf.Min(minDist, ManhattanDist(enemy.GridPos, d.GridPos));
            if (minDist != int.MaxValue) score -= minDist * OrderPerDistStep;
        }
        foreach (var t in GetAttackableUnits(enemy, enemy.GridPos, data))
        {
            if (t.Type == UnitType.门 || t.CurrentHP <= enemy.AttackPower)
            {
                score += OrderKillNowBonus;
                break;
            }
        }
        return score;
    }

    /// <summary>
    /// 狡诈弱侧门：防守最薄且 BFS 可达的玩家门（绕后偷家）。
    /// 防守强度 = 门旁（玩家 Stamina+AttackDistance 曼哈顿内）玩家单位的 1000 + 攻击力×100 之和；
    /// 同强度取更近的门；绕障不可达的死门排除。
    /// </summary>
    public static Unit PickWeakestDoor(AiBattleData data, Vector2I fromPos)
    {
        if (data?.PlayerDoors == null || data.PlayerDoors.Count == 0) return null;

        Unit best = null;
        int bestScore = int.MaxValue;
        foreach (var door in data.PlayerDoors)
        {
            if (door == null || !door.IsAlive || door.IsDead) continue;
            if (!PathFinder.GetDistanceFrom(door.GridPos, data.Map, fromPos, Team.Enemy).ContainsKey(fromPos))
                continue;   // 绕障不可达 → 绕不进去的死门排除

            int defense = 0;
            foreach (var p in data.PlayerUnits)
            {
                if (p == null || !p.IsAlive || p.IsDead) continue;
                if (ManhattanDist(p.GridPos, door.GridPos) <= p.Stamina + p.AttackDistance)
                    defense += 1000 + p.AttackPower * 100;
            }
            int score = defense + ManhattanDist(fromPos, door.GridPos);
            if (score < bestScore) { bestScore = score; best = door; }
        }
        return best;
    }

    // ======================================================================
    // 战场数据计算（纯函数）
    // ======================================================================
    /// <summary>玩家攻击威胁格集（所有存活玩家**单位**的**火力覆盖区域**，需攻击力&gt;0）。
    /// **不含门**——门是攻击目标不是威胁源：冲门挨门打是预期成本，门火力不纳入规避（否则前期弱敌人
    /// 因门有攻击力永远低血/全负效用而只会后退，2026-08-09 修复）。
    /// 用 GetAttackRange（范围内所有格）而非 GetAttackableTargets（只含当前有敌军的格）——威胁规避必须知道"哪些格会被打"。
    /// 形状感知：走玩家 AttackShape（CellShape 多态，菱形/方形/十字/射线等），public 供测试断言形状正确性。</summary>
    public static HashSet<Vector2I> ComputeThreatCells(Dictionary<Vector2I, Cell> map, List<Unit> playerUnits)
    {
        var cells = new HashSet<Vector2I>();
        foreach (var u in playerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            if (u.Type == UnitType.门) continue;   // 门不算威胁源
            if (u.AttackPower <= 0) continue;
            map.TryGetValue(u.GridPos, out Cell cell);
            var ctx = new Context { SourceUnit = u, Map = map, TargetCell = cell };
            foreach (var pos in PathFinder.GetAttackRange(u.GridPos, u.AttackShape, u.AttackDistance, map, ctx))
                cells.Add(pos);
        }
        return cells;
    }

    /// <summary>预测威胁区（狡诈）：玩家先移动（Stamina）再攻击（AttackDistance）能覆盖的格（曼哈顿估算，含墙会高估——狡诈宁可更谨慎）。
    /// lethalOnly=true 时仅计入攻击力 ≥ enemyHP 的单位（预测被击杀格）。**不含门**（同 ComputeThreatCells）。</summary>
    private static HashSet<Vector2I> ComputePredictedThreatCells(Dictionary<Vector2I, Cell> map, List<Unit> playerUnits, bool lethalOnly, int enemyHP)
    {
        var cells = new HashSet<Vector2I>();
        foreach (var u in playerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            if (u.Type == UnitType.门) continue;   // 门不算威胁源
            if (u.AttackPower <= 0) continue;
            if (lethalOnly && u.AttackPower < enemyHP) continue;

            int radius = u.Stamina + u.AttackDistance;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > radius) continue;
                    Vector2I p = u.GridPos + new Vector2I(dx, dy);
                    if (map.ContainsKey(p)) cells.Add(p);
                }
            }
        }
        return cells;
    }

    /// <summary>
    /// 压制位（2026-08-09 卡位正解）：**玩家攻击范围边缘外一格** = 距玩家曼哈顿距离恰为 AttackDistance+1 的环。
    /// 站这里下回合玩家想打必须先移动 → 浪费行动点（攻击不到/攻击完后的走位）。
    /// 精确一环而非"预测区\火力区"大环——大环会让敌人从远处就卡在环上与远程单位对峙（2026-08-09 修复）。
    /// 门不算（门是目标不是威胁）；攻击力 0 的玩家无压制位。
    /// </summary>
    private static HashSet<Vector2I> ComputePressureCells(Dictionary<Vector2I, Cell> map, List<Unit> playerUnits)
    {
        var cells = new HashSet<Vector2I>();
        foreach (var u in playerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            if (u.Type == UnitType.门) continue;
            if (u.AttackPower <= 0) continue;

            int radius = u.AttackDistance + 1;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius) continue;   // 恰为边缘外一格
                    Vector2I p = u.GridPos + new Vector2I(dx, dy);
                    if (map.ContainsKey(p)) cells.Add(p);
                }
            }
        }
        return cells;
    }

    /// <summary>玩家身边 4 格（贴身堵用：占据后玩家无法走到 → 缩玩家移动范围）。门不算。</summary>
    private static HashSet<Vector2I> ComputeAdjacentCells(Dictionary<Vector2I, Cell> map, List<Unit> playerUnits)
    {
        var cells = new HashSet<Vector2I>();
        foreach (var u in playerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            if (u.Type == UnitType.门) continue;
            foreach (var p in new[]
            {
                u.GridPos + Vector2I.Up, u.GridPos + Vector2I.Down,
                u.GridPos + Vector2I.Left, u.GridPos + Vector2I.Right,
            })
            {
                if (map.ContainsKey(p)) cells.Add(p);
            }
        }
        return cells;
    }

    /// <summary>从指定位置收集可攻击的存活玩家单位</summary>
    private static List<Unit> GetAttackableUnits(Unit enemy, Vector2I from, AiBattleData data)
    {
        var result = new List<Unit>();
        if (data?.Map == null || data.PlayerUnits == null) return result;

        data.Map.TryGetValue(from, out Cell cell);
        var ctx = new Context { SourceUnit = enemy, Map = data.Map, TargetCell = cell };
        foreach (var pos in PathFinder.GetAttackableTargets(from, enemy.AttackShape, enemy.AttackDistance, enemy.Team, data.Map, ctx))
        {
            if (!data.Map.TryGetValue(pos, out Cell c)) continue;
            var u = c.OccupyingUnit;
            if (u != null && u.IsAlive && !u.IsDead && u.Team == Team.Player)
                result.Add(u);
        }
        return result;
    }

    /// <summary>最近存活玩家单位（含门）</summary>
    private static Unit FindNearestPlayer(Vector2I from, AiBattleData data)
    {
        if (data?.PlayerUnits == null) return null;
        Unit nearest = null;
        int minDist = int.MaxValue;
        foreach (var u in data.PlayerUnits)
        {
            if (u == null || !u.IsAlive || u.IsDead) continue;
            int dist = ManhattanDist(from, u.GridPos);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = u;
            }
        }
        return nearest;
    }

    /// <summary>低血量判定：≤30% 最大生命，或 ≤ 玩家单位最高攻击力（一击即死）。**门不算**——门不会移动追杀，
    /// 冲门挨门打是预期成本，否则弱敌人因门有攻击力永远低血触发逃跑（2026-08-09 修复）。
    /// **初级炮灰永不逃跑**（牺牲值得，冲到底）。</summary>
    private static bool IsLowHp(Unit enemy, AiBattleData data)
    {
        if (IsExpendable(enemy)) return false;   // 初级：不做送死检查也永不逃跑
        if (enemy.CurrentHP <= enemy.MaxHP * LowHpRatio) return true;
        foreach (var p in data.PlayerUnits)
            if (p != null && p.Type != UnitType.门 && p.AttackPower >= enemy.CurrentHP) return true;
        return false;
    }

    /// <summary>到最近玩家**单位**的距离（逃跑用；**门不算**——门不追杀，场上只有门时不应逃跑）</summary>
    private static int DistToNearestPlayer(Vector2I pos, IReadOnlyCollection<Unit> playerUnits)
    {
        if (playerUnits == null) return 99;
        int min = 99;
        foreach (var p in playerUnits)
        {
            if (p == null || !p.IsAlive || p.IsDead) continue;
            if (p.Type == UnitType.门) continue;   // 门不算威胁源
            min = Mathf.Min(min, ManhattanDist(pos, p.GridPos));
        }
        return min;
    }

    /// <summary>曼哈顿距离</summary>
    public static int ManhattanDist(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }
}
