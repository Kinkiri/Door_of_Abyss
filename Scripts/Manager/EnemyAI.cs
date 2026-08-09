using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 敌方 AI 执行层：在 EnemyAction 阶段自动驱动作战单位行动。
/// 决策与执行分离——决策纯逻辑在 AiTactics（效用评分系统），本类只负责：
/// 收集战场快照（BuildBattleData）、狡诈行动顺序、集火状态维护、镜头预告、节奏控制（计时器/间隔）。
/// 决策按关卡 AI 等级（LevelData.AiLevel）门控：简单=无脑冲用光AP；标准=效用评分（火力区/刷怪格/卡位/留点）；
/// 狡诈=标准+集火/弱侧门/两步前瞻/预测威胁/行动顺序决策。
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

    /// <summary>
    /// 输出 AI 决策过程日志（[AI][决策] 前缀：战场摘要/候选评分分解/决策理由），调试用，默认关。
    /// 勾选后每回合 StartAITurn 时订阅 AiTactics.DebugLog（测试不订阅 → 无输出，纯逻辑可单测）。
    /// </summary>
    [Export] public bool DebugDecisions { get; set; } = false;

    private Queue<Unit> _actionQueue;

    /// <summary>本回合 AI 等级（StartAITurn 时从 LevelData 读取）</summary>
    private AiLevel _aiLevel = AiLevel.标准;

    /// <summary>各单位上回合移动起点（防 A↔B 来回动：移动决策排除该格）</summary>
    private readonly Dictionary<int, Vector2I> _lastMoveFrom = new();

    /// <summary>狡诈集火目标（攻击未打死的非门玩家单位；目标死亡/回合开始清除；决策侧只作 +4000 加成不锁死）</summary>
    private Unit _focusTarget;

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[EnemyAI] 就绪");
    }

    public override void _ExitTree()
    {
        _actionQueue?.Clear();
        if (Instance == this)
        {
            AiTactics.DebugLog = null;   // 解除日志钩子（防跨场景悬挂）
            Instance = null;
        }
    }

    public void Init() { }

    /// <summary>开始执行敌方回合</summary>
    public void StartAITurn()
    {
        _actionQueue = new Queue<Unit>();
        _focusTarget = null;
        // 决策日志钩子（DebugDecisions 勾选时输出 [AI][决策] 过程日志，方便观察 AI 在想什么）
        AiTactics.DebugLog = DebugDecisions ? (string m) => GD.Print(m) : null;
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

        var playerDoors = UnitManager.GetDoors(Team.Player).ToList();
        if (_aiLevel == AiLevel.狡诈)
        {
            // 狡诈：单位行动顺序决策——按粗效用降序（能击杀/打门、高攻、近门先手）
            var data = BuildBattleData();
            tempList.Sort((a, b) =>
            {
                int sa = AiTactics.RoughPriority(a, data);
                int sb = AiTactics.RoughPriority(b, data);
                if (sa != sb) return sb.CompareTo(sa);
                return DistToNearestDoor(a.GridPos, playerDoors).CompareTo(DistToNearestDoor(b.GridPos, playerDoors));
            });
        }
        else
        {
            // 简单/标准：按离最近玩家门距离排序（近的先行动）
            tempList.Sort((a, b) =>
                DistToNearestDoor(a.GridPos, playerDoors).CompareTo(DistToNearestDoor(b.GridPos, playerDoors)));
        }

        string doorInfo = playerDoors.Count > 0
            ? string.Join(", ", playerDoors.Select(d => $"{d.UnitData?.UnitName}@{d.GridPos}"))
            : "无存活门";
        GD.Print($"[EnemyAI] 敌方总计 {totalEnemy}，可行动 {tempList.Count} (玩家门: {doorInfo})");

        _actionQueue = new Queue<Unit>(tempList);

        if (_actionQueue.Count == 0)
        {
            GD.Print("[EnemyAI] 无可行动敌方单位，推进阶段");
            BattleManager.Instance?.AdvancePhase();
            return;
        }

        ProcessNext();
    }

    private void ProcessNext()
    {
        // 场景已卸载：跨场景存活的 SceneTree 计时器回调直接退出（Instance 已置空或指向新场景实例）
        if (Instance != this) return;

        if (_actionQueue.Count == 0)
        {
            GD.Print("[EnemyAI] 全部处理完毕，0.3s 后推进阶段");
            // processAlways:false —— 树暂停（Esc 暂停）时 AI 计时器停止，真实暂停
            var timer = GetTree().CreateTimer(0.3f, processAlways: false);
            timer.Timeout += () =>
            {
                if (Instance != this) return;
                BattleManager.Instance?.AdvancePhase();
            };
            return;
        }

        var enemy = _actionQueue.Dequeue();

        // 决策（纯读，AiTactics 效用评分）→ 预告镜头 → 停顿让摄像机跑过去 → 再执行行动
        var action = DecideAction(enemy);
        if (action == null || action.Kind == AiActionKind.Skip)
        {
            // 无有价值行动：结束该单位本回合（不重新入队，防 AP>0 死循环），保留行动点
            GD.Print($"[EnemyAI] {enemy.UnitData?.UnitName} {action?.Reason ?? "无可行动作"}（保留 AP {enemy.ActionPoints}）");
            var next = GetTree().CreateTimer(ActionDelay, processAlways: false);
            next.Timeout += ProcessNext;
            return;
        }

        PreviewCamera(enemy, action);
        var pan = GetTree().CreateTimer(CameraPanDelay, processAlways: false);
        pan.Timeout += () =>
        {
            // 场景已卸载：放弃过期行动计划（防跨场景误执行）
            if (Instance != this) return;
            ExecutePlan(enemy, action);
            // 只处理一个动作，如果还有剩余 AP 则重新入队（下一决策状态新鲜：位置/血量已更新）
            if (enemy.ActionPoints > 0)
                _actionQueue.Enqueue(enemy);
            var next = GetTree().CreateTimer(ActionDelay, processAlways: false);
            next.Timeout += ProcessNext;
        };
    }

    /// <summary>构建战场快照（每决策重建——前一单位行动后状态新鲜；纯数据零 Manager 依赖，供 AiTactics 读）</summary>
    private AiBattleData BuildBattleData()
    {
        var players = UnitManager.Instance.ActiveUnits
            .Where(u => u.Team == Team.Player && u.IsAlive && !u.IsDead).ToList();

        Unit focus = null;
        if (_aiLevel == AiLevel.狡诈 && _focusTarget != null && _focusTarget.IsAlive && !_focusTarget.IsDead)
            focus = _focusTarget;

        return new AiBattleData
        {
            Map = MapManager.Instance.Map,
            PlayerUnits = players,
            PlayerDoors = players.Where(u => u.Type == UnitType.门).ToList(),
            SpawnCells = _aiLevel != AiLevel.简单
                ? new HashSet<Vector2I>(BattleManager.Instance?.NextWaveSpawnPositions() ?? Enumerable.Empty<Vector2I>())
                : null,
            FocusTarget = focus,
            Level = _aiLevel,
        };
    }

    /// <summary>决策单个动作（一次攻击或一次移动），返回行动计划；不执行。</summary>
    private AiAction DecideAction(Unit enemy)
    {
        if (!enemy.IsAlive || enemy.IsDead || enemy.ActionPoints <= 0)
            return null;

        var data = BuildBattleData();
        data.PreviousPos = _lastMoveFrom.TryGetValue(enemy.ID, out var prev) ? prev : (Vector2I?)null;
        return AiTactics.DecideAction(enemy, data);
    }

    /// <summary>预告摄像机（发事件，View 层订阅驱动镜头）：攻击 → 行动单位+目标单位中点；移动 → 行动单位+目标格中点</summary>
    private void PreviewCamera(Unit enemy, AiAction action)
    {
        if (action.Kind == AiActionKind.Attack)
            AiAttackPreviewed?.Invoke(enemy, action.Target);
        else
            AiMovePreviewed?.Invoke(enemy, action.MovePos.Value);
    }

    /// <summary>执行已决策的行动（停顿结束后调用）</summary>
    private void ExecutePlan(Unit enemy, AiAction action)
    {
        var bm = BattleManager.Instance;
        if (action.Kind == AiActionKind.Attack)
        {
            bm?.AIDoAttack(enemy, action.Target);
            UpdateFocus(action.Target);
        }
        else
        {
            // 记录移动起点（供下回合防来回：禁止移回该格）
            _lastMoveFrom[enemy.ID] = enemy.GridPos;
            bm?.AIDoMove(enemy, action.MovePos.Value);
        }
    }

    /// <summary>集火状态维护（狡诈）：攻击未打死的非门玩家 → 成为集火目标（目标死亡自动清除，见 BuildBattleData 存活检查）</summary>
    private void UpdateFocus(Unit target)
    {
        if (_aiLevel != AiLevel.狡诈 || target == null) return;
        if (target.IsAlive && !target.IsDead && target.Team == Team.Player && target.Type != UnitType.门)
            _focusTarget = target;
    }

    private static int DistToNearestDoor(Vector2I pos, List<Unit> playerDoors)
    {
        if (playerDoors.Count == 0) return 0;
        int min = int.MaxValue;
        foreach (var d in playerDoors)
            min = Mathf.Min(min, AiTactics.ManhattanDist(pos, d.GridPos));
        return min;
    }
}
