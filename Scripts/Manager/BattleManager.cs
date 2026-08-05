using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 胜利条件委托
/// 返回 true 表示该方达成了胜利目标
/// </summary>
public delegate bool WinCondition();

/// <summary>
/// 战斗管理器，负责追踪战斗阶段和阵营轮换
/// 不实现状态机——仅维护阶段序列和阵营数据，外部系统监听 PhaseChanged 信号自行决定行为
/// </summary>
public partial class BattleManager : Node2D
{
    #region Singleton & Properties

    public static BattleManager Instance { get; private set; }

    /// <summary>自动推进间隔（秒），仅对非交互阶段生效</summary>
    private const float AutoAdvanceDelay = 0.5f;

    private SceneTreeTimer _autoAdvanceTimer;

    public BattlePhase CurrentPhase { get; private set; } = BattlePhase.GameStart;

    public Team CurrentTeam { get; private set; } = Team.Neutral;

    public int RoundCount { get; private set; } = 0;

    public bool IsGameOver => CurrentPhase == BattlePhase.GameEnd;

    /// <summary>是否处于玩家手动放门阶段</summary>
    public bool IsPlacingDoor { get; private set; } = false;

    /// <summary>放门区域格子（供 MapView 渲染）</summary>
    public HashSet<Vector2I> LastDoorPlaceZone { get; private set; }

    /// <summary>记录当前放到第几个门</summary>
    private int _placingDoorIndex = 0;

    /// <summary>
    /// 预计算的波次刷怪计划：回合 → 刷怪位置与单位。
    /// 上回合结束预计算并显示预告，本回合开始按计划生成（不再随机）。
    /// </summary>
    private readonly Dictionary<int, List<(Vector2I pos, UnitData data)>> _waveSpawnPlan = new();

    public Team Winner { get; private set; } = Team.Neutral;

    /// <summary>胜利条件（可替换），默认：敌方全灭</summary>
    public WinCondition PlayerWinCondition { get; set; } = DefaultPlayerWin;

    /// <summary>胜利条件（可替换），默认：玩家方全灭</summary>
    public WinCondition EnemyWinCondition { get; set; } = DefaultEnemyWin;

    // ── 费用系统 ─────────────────────────────────────────────────────────

    /// <summary>费用上限</summary>
    public const int MaxCost = 10;

    /// <summary>当前可用费用</summary>
    public int PlayerCost { get; private set; } = 0;

    /// <summary>
    /// 公开修改费用接口，供 ModifyCostAction 等外部系统调用。
    /// 自动钳制在 [0, MaxCost] 并发送 CostChanged 信号。
    /// </summary>
    public void ModifyPlayerCost(int delta)
    {
        int oldCost = PlayerCost;
        PlayerCost = Mathf.Clamp(PlayerCost + delta, 0, MaxCost);
        if (PlayerCost != oldCost)
            EmitSignal(SignalName.CostChanged, PlayerCost, MaxCost);
    }

    // ── 关卡配置 ────────────────────────────────────────────────────────

    [Export] public LevelData LevelData { get; set; }

    /// <summary>玩家全局数据（含卡组）</summary>
    [Export] public PlayerData PlayerData { get; set; }

    #endregion

    override public void _Ready()
    {
        Instance = this;
        // 通过主界面选关进入时，覆盖 Level.tscn 中的固定引用
        if (LevelSelection.Selected != null)
        {
            LevelData = LevelSelection.Selected;
            GD.Print($"[Battle] 选关加载: {LevelData.LevelName}");
        }
    }

    public void Init()
    {
        DoSubscribeSelection();
        EnterPhase(BattlePhase.GameStart);
    }

    /// <summary>
    /// 订阅 SelectionManager 的行为请求事件。
    /// SelectionManager 仅处理输入和范围计算，具体行为执行委托给 BattleManager。
    /// 这是"事件驱动"架构的核心解耦点。
    /// InitManager 保证所有 Instance 就绪后才调用此方法。
    /// </summary>
    private void DoSubscribeSelection()
    {
        var sm = SelectionManager.Instance;
        if (sm == null) return;
        sm.UnitMoveRequest += OnUnitMove;
        sm.UnitAttackRequest += OnUnitAttack;
        sm.CardPlayRequest += OnCardPlay;
        GD.Print("[Battle] SelectionManager 事件订阅完成");
    }

    // ======================================================================
    // 行为执行（响应 SelectionManager 事件）
    // ======================================================================

    #region 行为执行

    private void OnUnitMove(Unit unit, Vector2I targetPos)
    {
        if (unit.Team != CurrentTeam)
        {
            GD.Print("[Battle] 非当前行动阵营，无法移动");
            SelectionManager.Instance.ClearSelection();
            return;
        }
        if (unit.ActionPoints <= 0)
        {
            GD.Print($"[Battle] {unit.UnitData?.UnitName} 已无行动次数");
            SelectionManager.Instance.ClearSelection();
            return;
        }

        var oldPos = unit.GridPos;   // MoveUnit 前记录旧格（供事件载荷 SourceCell 使用）
        if (!UnitManager.Instance.MoveUnit(unit, targetPos)) return;

        unit.ActionPoints--;
        BuffManager.Instance?.MarkActionStart(unit);
        unit.ActionsThisTurn++;
        unit.UpdateUnit();
        GD.Print($"[Battle] 移动单位至 ({targetPos.X}, {targetPos.Y})，剩余 AP: {unit.ActionPoints}");

        // 事件载荷：SourceUnit=行动单位、TargetCell=到达格、SourceCell=原格（克隆式透传依赖调用点填全）
        var targetCell = MapManager.Instance.TryGetCell(targetPos, out var tc) ? tc : null;
        var oldCell = MapManager.Instance.TryGetCell(oldPos, out var oc) ? oc : null;
        EventBus.Instance?.Fire(EventType.OnUnitAct, new Context
        {
            ActType = UnitActType.Move,
            SourceUnit = unit,
            TargetCell = targetCell,
            SourceCell = oldCell,
        }, instigator: unit);
        EventBus.Instance?.Fire(EventType.OnMove, new Context
        {
            SourceUnit = unit,
            TargetCell = targetCell,
            SourceCell = oldCell,
        }, instigator: unit);
        UnitActed?.Invoke(unit);

        // AP 耗尽不取消选中（保留面板与范围显示供查看）——MoveUnit 已触发 UpdateUnit→RecalculateRanges，此处显式刷新双保险
        SelectionManager.Instance.RecalculateRanges();
    }

    private void OnUnitAttack(Unit attacker, Unit target)
    {
        if (attacker.Team != CurrentTeam)
        {
            GD.Print("[Battle] 非当前行动阵营，无法攻击");
            SelectionManager.Instance.ClearSelection();
            return;
        }
        if (attacker.ActionPoints <= 0)
        {
            GD.Print($"[Battle] {attacker.UnitData?.UnitName} 已无行动次数");
            SelectionManager.Instance.ClearSelection();
            return;
        }

        // 先扣 AP（即时生效，防止动画期间单位被重复操作）
        attacker.ActionPoints--;
        BuffManager.Instance?.MarkActionStart(attacker);
        GD.Print($"[Battle] {attacker.UnitData?.UnitName} 攻击 {target.UnitData?.UnitName}" +
                 $"，造成 {attacker.AttackPower} 点伤害，剩余 AP: {attacker.ActionPoints}");

        // 通过 DamageAction + ActionQueue 执行伤害（触发动画）
        var dmgAction = new DamageAction
        {
            Value = attacker.AttackPower,
            AnimationDuration = 0.3f,
        };
        var ctx = new Context
        {
            TargetUnit = target,
            TargetUnits = new[] { target },
            SourceUnit = attacker,
            SourceTeam = attacker.Team,
        };

        ActionQueue.Instance.Enqueue(new[] { dmgAction }, ctx, Callable.From(() =>
        {
            attacker.UpdateUnit();
            attacker.ActionsThisTurn++;
            CheckVictory();
            // 事件载荷：SourceUnit=攻击者、TargetUnit=受击者、格子（手牌被动靠 SourceUnit 识别发起方）
            EventBus.Instance?.Fire(EventType.OnUnitAct, new Context
            {
                ActType = UnitActType.Attack,
                SourceUnit = attacker,
                TargetUnit = target,
                SourceCell = MapManager.Instance.TryGetCell(attacker.GridPos, out var ac) ? ac : null,
                TargetCell = MapManager.Instance.TryGetCell(target.GridPos, out var tcc) ? tcc : null,
                AttackDirection = TargetResolver.DirectionBetween(attacker.GridPos, target.GridPos),
            }, instigator: attacker);
            UnitActed?.Invoke(attacker);

            // AP 耗尽不取消选中（保留面板与范围显示供查看）
            SelectionManager.Instance.RecalculateRanges();
        }));
    }

    private void OnCardPlay(Card card, Context ctx)
    {
        GD.Print($"[Battle] 使用卡牌: [{card.CardID}] {card.CardName}，费用: {card.Cost}");

        // 防御：出牌仅限玩家行动阶段（SelectionManager 已前置拦截，此处兜底其他调用路径）
        if (CurrentTeam != Team.Player)
        {
            GD.Print("[Battle] 非玩家行动阶段，不能出牌");
            SelectionManager.Instance.ClearSelection();
            return;
        }

        if (card.Cost > PlayerCost)
        {
            GD.Print($"[Battle] 费用不足！需要 {card.Cost}，当前 {PlayerCost}");
            SelectionManager.Instance.ClearSelection();
            return;
        }

        // ── 卡牌条件检查 ──────────────────────────────────────────
        if (card.CardData?.Conditions != null)
        {
            bool met = true;
            foreach (var c in card.CardData.Conditions)
            {
                if (c != null && !c.IsMet(ctx))
                {
                    met = false;
                    break;
                }
            }
            if (!met)
            {
                GD.Print($"[Battle] 卡牌条件不满足: {card.CardName}，取消出牌");
                SelectionManager.Instance.ClearSelection();
                return;
            }
        }

        PlayerCost -= card.Cost;
        EmitSignal(SignalName.CostChanged, PlayerCost, MaxCost);
        GD.Print($"[Battle] 扣除 {card.Cost} 费，剩余 {PlayerCost}/{MaxCost}");

        CardManager.Instance.UseCard(card);

        if (card.CardData?.Actions == null) return;

        ctx.SourceUnit = SelectionManager.Instance?.SelectedUnit;
        ctx.SourceTeam = BattleManager.Instance.CurrentTeam;

        // 出牌成功（扣费+移出手牌）即触发 OnUseCard，被动先于卡牌动作执行
        EventBus.Instance?.Fire(EventType.OnUseCard, ctx, instigator: ctx.SourceUnit);

        // 通过 ActionQueue 逐个执行，支持动画节奏；完成后回调（携带 card/ctx 用于选中新召唤单位）
        ActionQueue.Instance?.Enqueue(card.CardData.Actions, ctx, Callable.From(() => OnCardPlayActionsDone(card, ctx)));
    }

    private void OnCardPlayActionsDone(Card card, Context ctx)
    {
        GD.Print("[Battle] 卡牌动作序列执行完毕");
        CheckVictory();

        // 单位卡：放置成功后自动选中新召唤的单位（连带触发移动攻击高亮 + 摄像机聚焦）
        if (card.CardData is UnitCardData
            && ctx?.TargetCell?.OccupyingUnit is Unit spawned
            && spawned.IsAlive && !spawned.IsDead)
        {
            SelectionManager.Instance.SelectUnit(spawned);
        }
    }

    /// <summary>AI 移动单位（跳过阵营检查）</summary>
    public void AIDoMove(Unit unit, Vector2I targetPos)
    {
        if (unit.ActionPoints <= 0)
        {
            GD.Print($"[Battle][AI] {unit.UnitData?.UnitName} 无AP，跳过移动");
            return;
        }
        var oldPos = unit.GridPos;   // MoveUnit 前记录旧格（供事件载荷 SourceCell 使用）
        if (!UnitManager.Instance.MoveUnit(unit, targetPos))
        {
            GD.Print($"[Battle][AI] {unit.UnitData?.UnitName} MoveUnit 失败: {unit.GridPos} → {targetPos}");
            return;
        }

        unit.ActionPoints--;
        BuffManager.Instance?.MarkActionStart(unit);
        unit.ActionsThisTurn++;
        unit.UpdateUnit();
        GD.Print($"[Battle][AI] 移动 {unit.UnitData?.UnitName} 至 ({targetPos.X}, {targetPos.Y})");

        // 事件载荷：SourceUnit=行动单位、TargetCell=到达格、SourceCell=原格
        var targetCell = MapManager.Instance.TryGetCell(targetPos, out var tc) ? tc : null;
        var oldCell = MapManager.Instance.TryGetCell(oldPos, out var oc) ? oc : null;
        EventBus.Instance?.Fire(EventType.OnUnitAct, new Context
        {
            ActType = UnitActType.Move,
            SourceUnit = unit,
            TargetCell = targetCell,
            SourceCell = oldCell,
        }, instigator: unit);
        EventBus.Instance?.Fire(EventType.OnMove, new Context
        {
            SourceUnit = unit,
            TargetCell = targetCell,
            SourceCell = oldCell,
        }, instigator: unit);
        UnitActed?.Invoke(unit);
    }

    /// <summary>AI 攻击单位（跳过阵营检查）</summary>
    public void AIDoAttack(Unit attacker, Unit target)
    {
        if (attacker.ActionPoints <= 0) return;

        // 先扣 AP（即时生效）
        attacker.ActionPoints--;
        BuffManager.Instance?.MarkActionStart(attacker);
        GD.Print($"[Battle][AI] {attacker.UnitData?.UnitName} 攻击 {target.UnitData?.UnitName}，造成 {attacker.AttackPower} 点伤害");

        var dmgAction = new DamageAction
        {
            Value = attacker.AttackPower,
            AnimationDuration = 0.3f,
        };
        var ctx = new Context
        {
            TargetUnit = target,
            TargetUnits = new[] { target },
            SourceUnit = attacker,
            SourceTeam = attacker.Team,
        };

        ActionQueue.Instance.Enqueue(new[] { dmgAction }, ctx, Callable.From(() =>
        {
            attacker.UpdateUnit();
            attacker.ActionsThisTurn++;
            CheckVictory();
            // 事件载荷：SourceUnit=攻击者、TargetUnit=受击者、格子
            EventBus.Instance?.Fire(EventType.OnUnitAct, new Context
            {
                ActType = UnitActType.Attack,
                SourceUnit = attacker,
                TargetUnit = target,
                SourceCell = MapManager.Instance.TryGetCell(attacker.GridPos, out var ac) ? ac : null,
                TargetCell = MapManager.Instance.TryGetCell(target.GridPos, out var tcc) ? tcc : null,
                AttackDirection = TargetResolver.DirectionBetween(attacker.GridPos, target.GridPos),
            }, instigator: attacker);
            UnitActed?.Invoke(attacker);
        }));
    }

    #endregion
    // ======================================================================

    [Signal] public delegate void PhaseChangedEventHandler(BattlePhase newPhase, Team currentTeam, int round);
    [Signal] public delegate void GameEndedEventHandler(Team winner, int round);
    [Signal] public delegate void CostChangedEventHandler(int currentCost, int maxCost);

    /// <summary>任意单位（玩家/AI）完成一次移动或攻击行动时触发（View 层订阅，如摄像机跟随）</summary>
    public event Action<Unit> UnitActed;

    // ======================================================================

    #region 阶段推进
    /// <summary>
    /// 推进到下一个阶段，如果当前阶段无法推进则返回 false
    /// </summary>
    /// <returns></returns>
    public bool AdvancePhase()
    {
        BattlePhase next = GetNextPhase(CurrentPhase);
        if (next == CurrentPhase) return false;

        // 跳过没有活动单位的阶段（如无人可行动的 EnemyAction），最多跳 3 次防死循环
        int skipGuard = 0;
        while (!CanEnterPhase(next) && skipGuard < 3)
        {
            GD.Print($"[Battle] {next} 无法进入（无活动单位），跳过");
            next = GetNextPhase(next);
            skipGuard++;
        }

        return EnterPhase(next);
    }
    /// <summary>
    /// 尝试进入指定阶段，如果当前阶段无法进入则返回 false
    /// </summary>
    /// <param name="phase"></param>
    /// <returns></returns>
    public bool EnterPhase(BattlePhase phase)
    {
        if (!CanEnterPhase(phase)) return false;

        CancelAutoAdvance();
        BattlePhase prevPhase = CurrentPhase;
        OnPhaseExit(prevPhase);
        CurrentPhase = phase;
        OnPhaseEnter(phase);

        EmitSignal(SignalName.PhaseChanged, (int)phase, (int)CurrentTeam, RoundCount);
        GD.Print($"[Battle] 阶段切换 → {phase} | 队伍: {CurrentTeam} | 回合: {RoundCount}");
        return true;
    }

    #endregion

    // ======================================================================
    // 胜利判定
    // ======================================================================

    #region 胜利判定

    /// <summary>
    /// 检查双方胜利条件。
    /// 仅在攻击动作（玩家或 AI）完成后调用，不每帧执行。
    /// 胜利后自动进入 GameEnd 阶段并发射 GameEnded 信号。
    /// </summary>
    public void CheckVictory()
    {
        if (IsGameOver) return;

        if (PlayerWinCondition?.Invoke() == true)
        {
            Winner = Team.Player;
            GD.Print("[Battle] 玩家方达成胜利条件");
            EndGame();
            return;
        }

        if (EnemyWinCondition?.Invoke() == true)
        {
            Winner = Team.Enemy;
            GD.Print("[Battle] 敌方达成胜利条件");
            EndGame();
            return;
        }
    }
    /// <summary>
    /// 强制结束战斗，进入 GameEnd 阶段
    /// </summary>
    private void EndGame()
    {
        EnterPhase(BattlePhase.GameEnd);
        EmitSignal(SignalName.GameEnded, (int)Winner, RoundCount);
    }

    #endregion

    // ======================================================================
    // 内部逻辑
    // ======================================================================

    #region 内部逻辑

    /// <summary>
    /// 安排 0.5s 后自动推进到下一阶段。
    /// 仅对非交互阶段（GameStart / RoundStart / RoundEnd）调用。
    /// </summary>
    private void ScheduleAutoAdvance()
    {
        CancelAutoAdvance();
        _autoAdvanceTimer = GetTree().CreateTimer(AutoAdvanceDelay);
        _autoAdvanceTimer.Timeout += OnAutoAdvanceTimeout;
    }

    private void OnAutoAdvanceTimeout()
    {
        AdvancePhase();
    }

    private void CancelAutoAdvance()
    {
        if (_autoAdvanceTimer != null)
        {
            _autoAdvanceTimer.Timeout -= OnAutoAdvanceTimeout;
            _autoAdvanceTimer = null;
        }
    }

    private static BattlePhase GetNextPhase(BattlePhase from)
    {
        return from switch
        {
            BattlePhase.GameStart    => BattlePhase.RoundStart,
            BattlePhase.RoundStart   => BattlePhase.PlayerAction,
            BattlePhase.PlayerAction => BattlePhase.EnemyAction,
            BattlePhase.EnemyAction  => BattlePhase.RoundEnd,
            BattlePhase.RoundEnd     => BattlePhase.RoundStart,
            BattlePhase.GameEnd      => BattlePhase.GameEnd,
            _                        => BattlePhase.GameStart,
        };
    }

    private static bool CanEnterPhase(BattlePhase phase)
    {
        return phase switch
        {
            BattlePhase.PlayerAction => ActiveTeamUnits(Team.Player),
            BattlePhase.EnemyAction  => ActiveTeamUnits(Team.Enemy),
            _                        => true,
        };
    }

    #endregion

    // ======================================================================
    // 阶段进入 / 退出
    // ======================================================================

    #region 阶段进入/退出

    private void OnPhaseExit(BattlePhase phase)
    {
        switch (phase)
        {
            case BattlePhase.GameStart:    OnExitGameStart();    break;
            case BattlePhase.RoundStart:   OnExitRoundStart();   break;
            case BattlePhase.PlayerAction: OnExitPlayerAction(); break;
            case BattlePhase.EnemyAction:  OnExitEnemyAction();  break;
            case BattlePhase.RoundEnd:     OnExitRoundEnd();     break;
            case BattlePhase.GameEnd:      OnExitGameEnd();      break;
        }
    }

    private void OnExitGameStart()    { }
    private void OnExitRoundStart()   { }
    private void OnExitPlayerAction() { }
    private void OnExitEnemyAction()  { }
    private void OnExitRoundEnd()     { }
    private void OnExitGameEnd()      { }

    private void OnPhaseEnter(BattlePhase phase)
    {
        switch (phase)
        {
            case BattlePhase.GameStart:    OnEnterGameStart();    break;
            case BattlePhase.RoundStart:   OnEnterRoundStart();   break;
            case BattlePhase.PlayerAction: OnEnterPlayerAction(); break;
            case BattlePhase.EnemyAction:  OnEnterEnemyAction();  break;
            case BattlePhase.RoundEnd:     OnEnterRoundEnd();     break;
            case BattlePhase.GameEnd:      OnEnterGameEnd();      break;
        }
    }

    private void OnEnterGameStart()
    {
        CurrentTeam = Team.Neutral;
        RoundCount = 0;
        PlayerCost = 0;
        EmitSignal(SignalName.CostChanged, PlayerCost, MaxCost);
        GD.Print($"[Battle] 费用初始化: {PlayerCost}/{MaxCost}");

        // 从关卡配置加载地图数据
        if (LevelData?.MapData != null)
        {
            GD.Print($"[Battle] 从 LevelData 加载地图: {LevelData.LevelName}");
            MapManager.Instance.LoadFromMapData(LevelData.MapData);
        }

        // 预计算第 1 波刷怪位置并预告（放门阶段即显示）
        PreviewWave(1);

        // 判断是否进入玩家手动放门模式
        var doors = PlayerData?.DoorDatas?.Where(d => d != null).ToArray();
        bool canPlaceDoor = doors is { Length: > 0 }
            && LevelData?.DoorPlaceZoneMin != LevelData?.DoorPlaceZoneMax;

        if (canPlaceDoor)
        {
            _placingDoorIndex = 0;
            StartDoorPlacement();
            return; // 不自动推进，等玩家放门
        }

        // 无手动放门 → 自动放置所有门
        AutoPlaceDoorsAndAdvance();
    }

    /// <summary>进入玩家手动放门阶段（第 _placingDoorIndex 个门）</summary>
    private void StartDoorPlacement()
    {
        IsPlacingDoor = true;
        var doors = PlayerData?.DoorDatas?.Where(d => d != null).ToArray();
        string doorName = doors != null && _placingDoorIndex < doors.Length ? doors[_placingDoorIndex].UnitName : "?";

        // 计算可放置区域
        LastDoorPlaceZone = new HashSet<Vector2I>();
        var min = LevelData.DoorPlaceZoneMin;
        var max = LevelData.DoorPlaceZoneMax;
        var map = MapManager.Instance?.Map;
        for (int x = min.X; x <= max.X; x++)
            for (int y = min.Y; y <= max.Y; y++)
                if (map != null && map.ContainsKey(new Vector2I(x, y)))
                    LastDoorPlaceZone.Add(new Vector2I(x, y));

        GD.Print($"[Battle] 放置门 [{_placingDoorIndex + 1}/{doors?.Length ?? 0}] {doorName}，可放置区域 {min}~{max} ({(LastDoorPlaceZone?.Count ?? 0)}格)");

        // 通知 MapView 刷新高亮
        SelectionManager.Instance?.EmitSignal(SelectionManager.SignalName.SelectionUpdated);
    }

    /// <summary>玩家点击放置一个门，放完后检查是否还有下一个</summary>
    private void PlacePlayerDoor(Vector2I gridPos)
    {
        if (!IsPlacingDoor) return;

        var cell = MapManager.Instance?.Map?.GetValueOrDefault(gridPos);
        if (cell == null || !cell.CanStand) return;
        if (!LastDoorPlaceZone.Contains(gridPos)) return;

        var doors = PlayerData?.DoorDatas?.Where(d => d != null).ToArray();
        if (doors == null || _placingDoorIndex >= doors.Length) return;

        var doorData = doors[_placingDoorIndex];
        var door = UnitManager.Instance.SpawnUnit(doorData, gridPos, Team.Player);
        if (door == null)
        {
            GD.PrintErr("[Battle] 放置玩家门失败");
            return;
        }

        GD.Print($"[Battle] 放置门 [{_placingDoorIndex + 1}/{doors.Length}] {doorData.UnitName} 于 {gridPos} ID={door.ID} HP={door.CurrentHP}");
        _placingDoorIndex++;

        // 清除高亮
        LastDoorPlaceZone = null;
        SelectionManager.Instance?.EmitSignal(SelectionManager.SignalName.SelectionUpdated);

        if (_placingDoorIndex < doors.Length)
        {
            // 还有下一个门要放
            StartDoorPlacement();
        }
        else
        {
            // 所有门放完
            IsPlacingDoor = false;
            FinishGameStart();
        }
    }

    /// <summary>不手动放门时，自动放置所有门并继续</summary>
    private void AutoPlaceDoorsAndAdvance()
    {
        var doors = PlayerData?.DoorDatas?.Where(d => d != null).ToArray();
        if (doors is { Length: > 0 })
        {
            var pos = LevelData?.DoorPlaceZoneMin ?? Vector2I.Zero;
            foreach (var doorData in doors)
            {
                var door = UnitManager.Instance.SpawnUnit(doorData, pos, Team.Player);
                GD.Print($"[Battle] 自动放置门 {doorData.UnitName} 于 {pos}: {(door != null ? $"ID={door.ID}" : "失败")}");
            }
        }
        FinishGameStart();
    }

    /// <summary>GameStart 公共后续：初始化卡组 → 抽牌 → 推进</summary>
    private void FinishGameStart()
    {
        // 游戏开始抽 2 张牌
        if (CardManager.Instance != null)
        {
            // 优先级：关卡固定卡组 > 玩家构筑卡组 > 默认随机
            DeckData activeDeck = LevelData?.LevelDeck ?? PlayerData?.PlayerDeck;

            GD.Print($"[Battle] 卡组来源: LevelDeck={(LevelData?.LevelDeck != null ? LevelData.LevelDeck.GetType().Name : "null")}" +
                     $" PlayerDeck={(PlayerData?.PlayerDeck != null ? PlayerData.PlayerDeck.GetType().Name : "null")}" +
                     $" active={(activeDeck != null ? activeDeck.GetType().Name : "null")}");

            if (activeDeck?.Cards != null && activeDeck.Cards.Length > 0)
            {
                var deck = new System.Collections.Generic.List<CardData>(activeDeck.Cards);
                CardManager.Instance.InitializeDrawPile(deck);
                GD.Print($"[Battle] 使用{(LevelData?.LevelDeck != null ? "关卡固定" : "玩家")}卡组 ({deck.Count} 张)");
            }
            else
            {
                GD.Print($"[Battle] 未配置卡组，走默认随机");
            }

            CardManager.Instance.DrawCards(2);
        }

        ScheduleAutoAdvance();
    }

    public override void _Input(InputEvent @event)
    {
        // 放门阶段：左键点击放置
        if (IsPlacingDoor && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var gridPos = MapManager.Instance?.WorldToGrid(GetGlobalMousePosition());
            if (gridPos.HasValue)
                PlacePlayerDoor(gridPos.Value);
        }
    }

    // ── 旧 SpawnDoors 已拆分到 OnEnterGameStart / AutoPlacePlayerDoorAndAdvance ──

    /// <summary>检查并生成当前回合的波次</summary>
    private void SpawnWaveForRound(int round)
    {
        if (LevelData?.Waves == null) return;

        // 优先使用预计算计划（上回合已预告位置）；无计划时回退随机生成（如编辑器直接运行）
        if (_waveSpawnPlan.TryGetValue(round, out var plan))
        {
            _waveSpawnPlan.Remove(round);
            MapView.Instance?.ClearWavePreview();
            SpawnFromPlan(plan, round);
            return;
        }

        foreach (var wave in LevelData.Waves)
        {
            if (wave == null || wave.Round != round) continue;
            GD.Print($"[Battle] 回合 {round} 波次开始，要生成 {wave.UnitDatas?.Length ?? 0} 个单位");

            if (wave.UnitDatas == null || wave.UnitDatas.Length == 0) continue;

            var cells = CollectSpawnableCells(wave);
            if (cells.Count == 0)
            {
                GD.PrintErr($"[Battle] 波次生成区域无可站立空格");
                continue;
            }

            // Fisher-Yates 洗牌，使单位在生成区域内随机分布，
            // 避免每波次单位总是从固定坐标出现
            Shuffle(cells);

            int spawned = 0;
            for (int i = 0; i < wave.UnitDatas.Length && i < cells.Count; i++)
            {
                if (wave.UnitDatas[i] == null)
                {
                    GD.PrintErr($"[Battle] 波次 {wave.Round} 第 {i} 个 UnitData 为 null，跳过");
                    continue;
                }
                var unit = UnitManager.Instance.SpawnUnit(wave.UnitDatas[i], cells[i], Team.Enemy);
                if (unit != null) spawned++;
            }

            GD.Print($"[Battle] 波次生成完成：成功 {spawned}/{wave.UnitDatas.Length}");
        }
    }

    /// <summary>按预计算计划生成波次单位（位置已在预告时锁定）</summary>
    private void SpawnFromPlan(List<(Vector2I pos, UnitData data)> plan, int round)
    {
        GD.Print($"[Battle] 回合 {round} 波次开始，按预告生成 {plan.Count} 个单位");
        int spawned = 0;
        foreach (var (pos, data) in plan)
        {
            if (data == null) continue;
            var unit = UnitManager.Instance.SpawnUnit(data, pos, Team.Enemy);
            if (unit != null) spawned++;
        }
        GD.Print($"[Battle] 波次生成完成：成功 {spawned}/{plan.Count}");
    }

    /// <summary>预计算第 round 波次的刷怪位置（洗牌锁定），并通知视图显示预告</summary>
    private void PreviewWave(int round)
    {
        if (LevelData?.Waves == null) return;
        var wave = Array.Find(LevelData.Waves, w => w != null && w.Round == round);
        if (wave == null || wave.UnitDatas == null || wave.UnitDatas.Length == 0) return;

        var cells = CollectSpawnableCells(wave);
        if (cells.Count == 0)
        {
            GD.PrintErr($"[Battle] 波次预告：回合 {round} 生成区域无可站立空格");
            return;
        }
        Shuffle(cells);

        var plan = new List<(Vector2I pos, UnitData data)>();
        int count = Math.Min(wave.UnitDatas.Length, cells.Count);
        for (int i = 0; i < count; i++)
            plan.Add((cells[i], wave.UnitDatas[i]));

        _waveSpawnPlan[round] = plan;
        MapView.Instance?.RenderWavePreview(plan);
        GD.Print($"[Battle] 预告回合 {round} 波次：{count} 个单位");
    }

    /// <summary>收集波次生成区域内可站立且未被占据的格子（WaveData 有自定义区域则用之，否则用 LevelData 默认）</summary>
    private List<Vector2I> CollectSpawnableCells(WaveData wave)
    {
        var areaMin = wave.SpawnAreaMin != Vector2I.Zero || wave.SpawnAreaMax != Vector2I.Zero
            ? wave.SpawnAreaMin : LevelData?.DefaultSpawnAreaMin ?? Vector2I.Zero;
        var areaMax = wave.SpawnAreaMin != Vector2I.Zero || wave.SpawnAreaMax != Vector2I.Zero
            ? wave.SpawnAreaMax : LevelData?.DefaultSpawnAreaMax ?? Vector2I.Zero;

        var cells = new List<Vector2I>();
        for (int x = areaMin.X; x <= areaMax.X; x++)
            for (int y = areaMin.Y; y <= areaMax.Y; y++)
            {
                var pos = new Vector2I(x, y);
                if (MapManager.Instance.TryGetCell(pos, out Cell c)
                    && c.CanStand && c.OccupyingUnit == null)
                    cells.Add(pos);
            }
        return cells;
    }

    /// <summary>Fisher-Yates 洗牌，使单位在生成区域内随机分布</summary>
    private static void Shuffle(List<Vector2I> list)
    {
        var rng = new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void OnEnterRoundStart()
    {
        RoundCount++;
        CurrentTeam = Team.Neutral;

        // 重置所有存活单位的行动次数（恢复满上限，不从模板取——Buff/装备加的上限在此生效）
        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.IsAlive && !u.IsDead)
            {
                u.ActionPoints = u.MaxActionPoints;
                u.ActionsThisTurn = 0;
            }
        }
        GD.Print($"[Battle] 回合 {RoundCount} 开始，所有单位行动次数已重置");

        // 收集所有我方门的 CostPerRound / DrawPerRound
        int totalCost = 0;
        int totalDraw = 0;
        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.IsAlive && !u.IsDead && u.Team == Team.Player && u.Type == UnitType.门
                && u.UnitData is DoorData doorData)
            {
                totalCost += doorData.CostPerRound;
                totalDraw += doorData.DrawPerRound;
            }
        }

        PlayerCost = Mathf.Min(PlayerCost + totalCost, MaxCost);
        EmitSignal(SignalName.CostChanged, PlayerCost, MaxCost);
        GD.Print($"[Battle] 费用回复 +{totalCost}（来自门），当前 {PlayerCost}/{MaxCost}");

        if (totalDraw > 0)
        {
            GD.Print($"[Battle] 抽牌 {totalDraw} 张（来自门），走 DrawCardAction 队列");
            ActionQueue.Instance?.Enqueue(
                new[] { new DrawCardAction { Value = totalDraw, AnimationDuration = 0.3f } },
                new Context());
        }

        // 检查本回合是否有波次
        SpawnWaveForRound(RoundCount);

        // 预告下一波刷怪位置：玩家在本回合行动阶段即可看到并布置防线
        PreviewWave(RoundCount + 1);

        // 触发回合开始被动事件
        EventBus.Instance?.Fire(EventType.RoundStart, new Context());

        // 重置被动效果触发计数
        EventBus.Instance?.ResetTriggerCounts();

        CheckVictory();

        ScheduleAutoAdvance();
    }

    private void OnEnterPlayerAction()
    {
        CurrentTeam = Team.Player;
    }

    private void OnEnterEnemyAction()
    {
        CurrentTeam = Team.Enemy;
        // CallDeferred 避免在阶段切换的事件处理循环中直接启动 EnemyAI
        //（涉及 UnitManager 遍历和 CreateTimer），确保当前帧 PhaseChanged 信号处理完毕
        CallDeferred(nameof(StartEnemyAI));
    }

    private void StartEnemyAI()
    {
        if (EnemyAI.Instance != null)
            EnemyAI.Instance.StartAITurn();
        else
            AdvancePhase();
    }

    private void OnEnterRoundEnd()
    {
        CurrentTeam = Team.Neutral;

        // 触发回合结束被动事件
        EventBus.Instance?.Fire(EventType.RoundEnd, new Context());

        // Buff 回合倒计时（tick 在 RoundEnd 被动之后，归零的 Buff 触发 OnExpireActions）
        BuffManager.Instance?.TickAllBuffs();

        // 环境回合倒计时（语义同 Buff：RoundEnd 被动之后，归零的环境触发 OnExpireActions）
        EnvironmentManager.Instance?.TickAllEnvironments();

        CheckVictory();

        ScheduleAutoAdvance();
    }

    private void OnEnterGameEnd()
    {
        CurrentTeam = Team.Neutral;
    }

    #endregion

    // ======================================================================
    // 默认胜利条件
    // ======================================================================

    #region 默认胜利条件

    private static bool DefaultPlayerWin()
    {
        // 条件1：所有波次已出完（当前回合数已超过最大波次回合）
        if (Instance.LevelData?.Waves != null && Instance.LevelData.Waves.Length > 0)
        {
            int maxWaveRound = 0;
            foreach (var wave in Instance.LevelData.Waves)
            {
                if (wave != null && wave.Round > maxWaveRound)
                    maxWaveRound = wave.Round;
            }
            if (Instance.RoundCount < maxWaveRound)
                return false;
        }

        // 条件2：场上没有敌方单位（排除敌方门）
        foreach (var u in UnitManager.Instance.ActiveUnits)
        {
            if (u.Team == Team.Enemy && u.IsAlive && !u.IsDead && u.Type != UnitType.门)
                return false;
        }

        return true;
    }

    private static bool DefaultEnemyWin()
    {
        return !UnitManager.GetDoors(Team.Player).Any(u => u.IsAlive && !u.IsDead);
    }

    private static bool ActiveTeamUnits(Team team)
    {
        foreach (var u in UnitManager.Instance.ActiveUnits)
            if (u.Team == team && u.IsAlive && !u.IsDead)
                return true;
        return false;
    }

    #endregion
}
