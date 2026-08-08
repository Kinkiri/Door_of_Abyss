using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 选中管理器：处理输入、维护选中状态、计算范围，通过事件通知 BattleManager 执行具体行为
/// </summary>
public partial class SelectionManager : Node2D
{
    public static SelectionManager Instance { get; private set; }

    // ── 选中数据 ────────────────────────────────────────────────────────

    public Unit SelectedUnit { get; private set; }
    public Cell SelectedCell { get; private set; }
    public HashSet<Vector2I> LastReachableCells => _reachable;
    public HashSet<Vector2I> LastAttackableTargets => _attackable;
    public HashSet<Vector2I> LastAttackRange => _attackRange;
    public HashSet<Vector2I> LastCardPreviewCells { get; private set; }
    public Card SelectedCard { get; private set; }

    private HashSet<Vector2I> _reachable;
    private HashSet<Vector2I> _attackable;
    private HashSet<Vector2I> _attackRange;

    public bool IsAimingMode { get; set; }

    // ── 事件（BattleManager 订阅后执行具体行为） ────────────────────────

    /// <summary>请求移动单位，参数为单位和目标格子</summary>
    public event Action<Unit, Vector2I> UnitMoveRequest;
    /// <summary>请求攻击，参数为攻击者和目标</summary>
    public event Action<Unit, Unit> UnitAttackRequest;
    /// <summary>请求出牌，参数为卡牌和上下文（含目标信息）</summary>
    public event Action<Card, Context> CardPlayRequest;

    [Signal] public delegate void SelectionUpdatedEventHandler();

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        var um = UnitManager.Instance;
        if (um == null) return;
        um.OnUnitSpawned -= OnAnyUnitChanged;
        um.OnUnitRemoved -= OnAnyUnitChanged;
        um.OnUnitMoved -= OnAnyUnitChanged;
    }

    public void Init()
    {
        // 场上单位出现/消失/移动 → 重算选中单位的高亮范围：
        // 攻击目标集合依赖场上单位分布，攻击范围依赖格子占据（排除友方），移动范围依赖可站立状态，
        // 其他单位的变化都会改变这些结果（如敌方移入射程、友方死亡释放格子、新单位生成）
        var um = UnitManager.Instance;
        if (um == null) return;
        um.OnUnitSpawned += OnAnyUnitChanged;
        um.OnUnitRemoved += OnAnyUnitChanged;
        um.OnUnitMoved += OnAnyUnitChanged;
    }

    private void OnAnyUnitChanged(Unit _) => RecalculateRanges();

    public override void _Input(InputEvent @event)
    {
        // 鼠标移动时更新卡牌目标预览区域
        if (@event is InputEventMouseMotion && SelectedCard != null)
        {
            var mousePos = GetGlobalMousePosition();
            var gridPos = MapManager.Instance.WorldToGrid(mousePos);
            UpdateCardPreview(gridPos);
        }
    }

    // ======================================================================
    // 外部调用
    // ======================================================================

    /// <summary>由 HandPanel 调用：选中卡牌（任意阶段可，用于查看信息），进入瞄准模式。
    /// 打出前置检查（玩家行动阶段 + 费用足够）在出牌点击时执行。</summary>
    public void OnCardClicked(Card card)
    {
        if (card == null) return;

        // 先取消单位/格子选中（清除移动攻击范围高亮），再进入卡牌瞄准模式，
        // 避免单位范围与卡牌目标预览叠加显示
        UnselectUnit();
        LastCardPreviewCells = null; // 换卡时清掉上一张卡的预览，等待鼠标移动重算

        SelectedCard = card;
        IsAimingMode = true;
        AudioManager.Instance?.PlayUiSfx("card_select");
        GD.Print($"[Selection] 选中卡牌: [{card.CardID}] {card.CardName}，请选择目标");
        // 通知 View 层（信息面板等）卡牌选中状态变化
        EmitSignal(SignalName.SelectionUpdated);
    }

    /// <summary>仅取消单位/格子选中（清除范围高亮与状态订阅），保留卡牌选中状态，供出牌瞄准场景复用</summary>
    private void UnselectUnit()
    {
        UnsubscribeSelectedUnit();
        SelectedUnit = null;
        SelectedCell = null;
        _reachable = null;
        _attackable = null;
        _attackRange = null;
    }

    /// <summary>由 BattleManager / 选中单位状态更新触发：重算移动与攻击范围。
    /// 任意阶段、AP 耗尽都保留范围显示（供查看）；单位死亡时取消选中。</summary>
    public void RecalculateRanges()
    {
        if (SelectedUnit == null)
        {
            _reachable = null;
            _attackable = null;
            _attackRange = null;
            EmitSignal(SignalName.SelectionUpdated);
            return;
        }

        // 单位死亡：取消选中（避免死单位范围/面板残留）
        if (SelectedUnit.IsDead || !SelectedUnit.IsAlive)
        {
            ClearSelection();
            return;
        }

        _reachable = PathFinder.GetReachableCellsWithAttackTargets(
            SelectedUnit.GridPos, SelectedUnit.Stamina,
            SelectedUnit.AttackShape, SelectedUnit.AttackDistance, SelectedUnit.Team,
            MapManager.Instance.Map, MakeAtkCtx(SelectedUnit), out _attackable);
        _attackRange = CalcAttackRange(SelectedUnit);
        EmitSignal(SignalName.SelectionUpdated);
    }

    /// <summary>构造攻击范围计算用的 ECA 上下文（形状值源可读 SourceUnit 属性，如射程联动）</summary>
    private static Context MakeAtkCtx(Unit unit)
    {
        var map = MapManager.Instance?.Map;
        Cell center = null;
        if (map != null)
            map.TryGetValue(unit.GridPos, out center);
        return new Context { SourceUnit = unit, Map = map, TargetCell = center };
    }

    // ======================================================================
    // 输入处理
    // ======================================================================

    /// <summary>
    /// 全局未处理输入事件。调度策略（按优先级）：
    /// 1. 右键 → 清除所有选中状态
    /// 2. 出牌模式（SelectedCard != null）→ 在点击位置执行卡牌
    /// 3. 点击单位 → 选中或攻击
    /// 4. 点击格子 → 移动或选中格子
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton m)
            GD.Print($"[Selection] _UnhandledInput: btn={m.ButtonIndex} pressed={m.Pressed} placingDoor={BattleManager.Instance?.IsPlacingDoor} card={SelectedCard?.CardID}");

        // 放门阶段不处理普通交互
        if (BattleManager.Instance?.IsPlacingDoor == true) return;

        if (!(@event is InputEventMouseButton mouse && mouse.Pressed)) return;

        if (mouse.ButtonIndex == MouseButton.Right)
        {
            GetViewport().SetInputAsHandled();
            GD.Print("[Selection] 右键取消选择");
            ClearSelection();
            return;
        }

        if (mouse.ButtonIndex != MouseButton.Left) return;

        // 出牌流程：点任意位置出牌
        if (SelectedCard != null)
        {
            var mousePos = GetGlobalMousePosition();
            var clickGrid = MapManager.Instance.WorldToGrid(mousePos);
            MapManager.Instance.TryGetCell(clickGrid, out Cell clickCell);

            var ctx = new Context
            {
                TargetCell = clickCell,
                TargetUnit = clickCell?.OccupyingUnit,
                SourceCard = SelectedCard,
            };

            bool canPlay = BattleManager.Instance?.CurrentTeam == Team.Player
                           && SelectedCard.Cost <= BattleManager.Instance.PlayerCost;

            if (canPlay && ValidateCardTarget(SelectedCard, ctx))
            {
                GD.Print($"[Selection] 出牌: [{SelectedCard.CardID}] {SelectedCard.CardName} 目标={clickGrid}");
                CardPlayRequest?.Invoke(SelectedCard, ctx);
                ClearSelection();
                return;
            }

            // 无法出牌（非玩家阶段 / 费用不足 / 目标无效）：取消出牌模式，作为普通点击继续处理
            // （点单位则选中单位、点格子则选中格子），避免卡牌信息残留在信息面板
            GD.Print("[Selection] 无法出牌（非玩家阶段/费用不足/目标无效），取消出牌模式，作为普通点击处理");
            AudioManager.Instance?.PlayUiSfx("deny");
            SelectedCard = null;
            IsAimingMode = false;
            LastCardPreviewCells = null;
            // 不 return——落入下方普通地图交互分支
        }

        // 正常地图交互
        Vector2 worldPos = GetGlobalMousePosition();
        Vector2I gridPos = MapManager.Instance.WorldToGrid(worldPos);

        if (!MapManager.Instance.HasCell(gridPos))
        {
            GD.Print($"[Selection] 点击空白区域 ({gridPos.X}, {gridPos.Y})");
            ClearSelection();
            return;
        }

        GetViewport().SetInputAsHandled();
        MapManager.Instance.TryGetCell(gridPos, out Cell cell);
        Unit clickedUnit = cell.OccupyingUnit;

        if (clickedUnit != null && !IsAimingMode)
        {
            HandleUnitClick(clickedUnit, gridPos);
            return;
        }

        HandleCellClick(gridPos, cell);
    }

    // ======================================================================
    // 卡牌目标验证
    // ======================================================================

    private bool ValidateCardTarget(Card card, Context ctx)
    {
        var tf = card.TargetFilter;
        if (tf == null) return true;
        var shape = tf.GetShape();

        // 无目标 / 全地图 → 直接通过
        if (shape == TargetShape.None || shape == TargetShape.All)
            return true;

        // 需要选择格子
        if (shape is TargetShape.SingleCell or TargetShape.AreaDiamond or TargetShape.AreaSquare
            or TargetShape.Cross or TargetShape.X or TargetShape.Ray or TargetShape.Triangle)
        {
            if (ctx.TargetCell == null)
            {
                GD.Print("[Selection] 需要指定一个格子");
                return false;
            }
            if (card.Type == CardType.Unit)
            {
                if (!ctx.TargetCell.CanStand)
                {
                    GD.Print("[Selection] 该格子不可站立");
                    return false;
                }
                if (ctx.TargetUnit != null)
                {
                    GD.Print("[Selection] 该格子已被单位占据");
                    return false;
                }
                // 部署限制前置拦截：单位卡只能在己方门部署范围内召唤（防止出牌后动作拒绝导致白亏卡牌）
                if (!IsWithinDeployRange(ctx.TargetCell.GridPos))
                {
                    GD.Print("[Selection] 该格子不在部署范围内");
                    return false;
                }
            }
            return true;
        }

        // 需要选择单位：走 TargetFilter 的完整匹配谓词（阵营/类型/标签/条件）
        if (shape == TargetShape.SingleUnit)
        {
            if (ctx.TargetUnit == null)
            {
                GD.Print("[Selection] 需要选择一个单位");
                return false;
            }
            var currentTeam = BattleManager.Instance?.CurrentTeam ?? Team.Neutral;
            var matchCtx = new Context
            {
                SourceUnit = ctx.SourceUnit,
                TargetUnit = ctx.TargetUnit,
                SourceTeam = currentTeam,
                TargetTeam = ctx.TargetUnit.Team,
                SourceCard = card,
                Map = ctx.Map,
                ActiveUnits = ctx.ActiveUnits,
            };
            if (!tf.IsUnitMatch(ctx.TargetUnit, currentTeam, matchCtx))
            {
                GD.Print("[Selection] 目标单位不满足卡牌目标筛选条件");
                return false;
            }
            return true;
        }

        return true;
    }

    // ======================================================================
    // 单位点击
    // ======================================================================

    private void HandleUnitClick(Unit clickedUnit, Vector2I gridPos)
    {
        // 任意阶段/任意单位都可选中查看（面板 + 移动攻击高亮）；
        // 仅玩家行动阶段且选中者仍有 AP 才触发攻击请求
        bool canAct = BattleManager.Instance?.CurrentTeam == Team.Player
                      && SelectedUnit != null
                      && SelectedUnit.ActionPoints > 0;

        // 已选 + 可攻击 → 请求攻击
        if (canAct && _attackable?.Contains(gridPos) == true)
        {
            UnitAttackRequest?.Invoke(SelectedUnit, clickedUnit);
            return;
        }

        // 同单位 → 取消
        if (SelectedUnit?.ID == clickedUnit.ID)
        {
            GD.Print($"[Selection] 取消选中单位 [ID={SelectedUnit.ID}]");
            ClearSelection();
            return;
        }

        // 选中（不论阵营，显示范围）
        SelectUnit(clickedUnit);
    }

    // ======================================================================
    // 格子点击
    // ======================================================================

    private void HandleCellClick(Vector2I gridPos, Cell cell)
    {
        bool canAct = BattleManager.Instance?.CurrentTeam == Team.Player;

        // 玩家行动阶段 + 选中单位有 AP：可达则移动，不可达则取消选中
        if (SelectedUnit != null && canAct && SelectedUnit.ActionPoints > 0)
        {
            if (_reachable?.Contains(gridPos) == true)
            {
                UnitMoveRequest?.Invoke(SelectedUnit, gridPos);
                return;
            }
            GD.Print("[Selection] 目标格子不可达，取消选中");
            ClearSelection();
            return;
        }

        // 非行动阶段（或选中单位无 AP/非当前行动方）：选中格子查看信息，清除单位选中
        UnsubscribeSelectedUnit();
        SelectedUnit = null;
        _reachable = null;
        _attackable = null;
        _attackRange = null;
        SelectedCell = cell;
        GD.Print($"[Selection] 选中格子 ({gridPos.X}, {gridPos.Y})");
        // 通知 View 层（信息面板等）选中格子已变化——单位选中在 SelectUnit 内发信号，此处补齐空格子场景
        EmitSignal(SignalName.SelectionUpdated);
    }

    // ======================================================================
    // 选中 + 范围计算
    // ======================================================================

    /// <summary>选中一个单位（任意阶段/任意单位可用，计算并显示移动与攻击范围）。
    /// 供点击与外部（如单位卡召唤后自动选中）调用。</summary>
    public void SelectUnit(Unit unit)
    {
        if (unit == null) return;

        UnsubscribeSelectedUnit();
        SelectedUnit = unit;
        SubscribeSelectedUnit(unit);
        SelectedCell = null;
        MapManager.Instance.TryGetCell(unit.GridPos, out Cell cell);
        SelectedCell = cell;

        GD.Print($"[Selection] 选中单位 [ID={unit.ID}] {unit.UnitData?.UnitName}");

        // 任意阶段 / AP 耗尽都计算并显示移动与攻击范围（供查看）
        RecalculateRanges();
    }

    /// <summary>计算攻击范围（排除友方格子；按 AttackShape 生成，null=默认菱形）</summary>
    private HashSet<Vector2I> CalcAttackRange(Unit unit)
    {
        var map = MapManager.Instance.Map;
        var raw = PathFinder.GetAttackRange(unit.GridPos, unit.AttackShape, unit.AttackDistance, map, MakeAtkCtx(unit));
        raw.RemoveWhere(pos =>
            map.TryGetValue(pos, out Cell c)
            && c.OccupyingUnit != null && c.OccupyingUnit.Team == unit.Team);
        return raw;
    }

    /// <summary>当鼠标悬停时更新卡牌目标预览区域</summary>
    private void UpdateCardPreview(Vector2I hoverGrid)
    {
        if (!MapManager.Instance.HasCell(hoverGrid))
        {
            if (LastCardPreviewCells != null)
            {
                LastCardPreviewCells = null;
                EmitSignal(SignalName.SelectionUpdated);
            }
            return;
        }

        var cells = ComputeCardPreview(hoverGrid, SelectedCard);
        // 去重比较，避免每帧都发信号
        if (!AreSetsEqual(cells, LastCardPreviewCells))
        {
            LastCardPreviewCells = cells;
            EmitSignal(SignalName.SelectionUpdated);
        }
    }

    /// <summary>根据卡牌 TargetFilter 计算预览格子集合，过滤无效目标</summary>
    private static HashSet<Vector2I> ComputeCardPreview(Vector2I center, Card card)
    {
        var tf = card.TargetFilter;
        if (tf == null) return null;
        var shape = tf.GetShape();
        if (shape == TargetShape.None)
            return null;

        var map = MapManager.Instance?.Map;
        if (map == null) return null;

        var currentTeam = BattleManager.Instance?.CurrentTeam ?? Team.Neutral;
        var previewCtx = new Context
        {
            SourceTeam = currentTeam,
            SourceCard = card,
            Map = map,
            ActiveUnits = UnitManager.Instance?.ActiveUnits,
        };

        // 全地图目标：根据 TargetFilter 只显示有匹配单位的格子
        if (shape == TargetShape.All)
        {
            var result = new HashSet<Vector2I>();
            foreach (var (pos, cell) in map)
            {
                var u = cell.OccupyingUnit;
                if (u == null || !u.IsAlive || u.IsDead) continue;
                if (!tf.IsUnitMatch(u, currentTeam, previewCtx)) continue;
                result.Add(pos);
            }
            return result.Count > 0 ? result : null;
        }

        if (!map.ContainsKey(center)) return null;

        // 点选单位：只显示有合法单位的位置
        if (shape == TargetShape.SingleUnit)
        {
            var cell = map[center];
            var unit = cell.OccupyingUnit;
            if (unit == null || !unit.IsAlive || unit.IsDead)
                return null;
            if (!tf.IsUnitMatch(unit, currentTeam, previewCtx))
                return null;
            return new HashSet<Vector2I> { center };
        }

        // 点选格子：按卡牌类型校验
        if (shape == TargetShape.SingleCell)
        {
            var cell = map[center];
            if (card.Type == CardType.Unit)
            {
                if (!cell.CanStand || cell.OccupyingUnit != null)
                    return null;
                // 部署限制：必须在门曼哈顿距离 2 以内
                if (!IsWithinDeployRange(center))
                    return null;
            }
            return new HashSet<Vector2I> { center };
        }

        // 自定义形状（CellShape 多态体系）：统一生成预览格（与解析共用同一算法）
        // 覆盖十字/叉/射线/三角形以及新配置的菱形/方形
        var cellShape = tf.GetCellShape();
        if (cellShape != null)
        {
            var shapeCells = cellShape.GetCells(map[center], previewCtx);
            if (shapeCells.Length == 0) return null;
            var set = new HashSet<Vector2I>();
            foreach (var c in shapeCells)
                set.Add(c.GridPos);
            return set;
        }

        // 范围形状（旧枚举路径，兼容存量资源）：显示所有在范围中的格子（区域本身即是目标）
        int range = tf.GetAreaRange();
        var cells = new HashSet<Vector2I>();

        if (shape == TargetShape.AreaDiamond)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) <= range)
                        AddIfInMap(cells, new Vector2I(center.X + dx, center.Y + dy), map);
        }
        else if (shape == TargetShape.AreaSquare)
        {
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    AddIfInMap(cells, new Vector2I(center.X + dx, center.Y + dy), map);
        }

        return cells.Count > 0 ? cells : null;
    }

    /// <summary>检查格子是否在己方门的部署范围内（任意一门范围内即可）</summary>
    private static bool IsWithinDeployRange(Vector2I gridPos)
    {
        foreach (var door in UnitManager.GetDoors(Team.Player))
        {
            int range = (door.UnitData as DoorData)?.DeployRange ?? 2;
            int dist = System.Math.Abs(gridPos.X - door.GridPos.X) +
                       System.Math.Abs(gridPos.Y - door.GridPos.Y);
            if (dist <= range) return true;
        }
        return false; // 没有门时不允许部署
    }

    private static void AddIfInMap(HashSet<Vector2I> cells, Vector2I pos, Dictionary<Vector2I, Cell> map)
    {
        if (map.ContainsKey(pos))
            cells.Add(pos);
    }

    private static bool AreSetsEqual(HashSet<Vector2I> a, HashSet<Vector2I> b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        a.Overlaps(b); // 无关，只是用一下不含副作用的操作
        return a.SetEquals(b);
    }

    public void ClearSelection()
    {
        UnselectUnit();
        SelectedCard = null;
        IsAimingMode = false;
        LastCardPreviewCells = null;
        EmitSignal(SignalName.SelectionUpdated);
    }

    // ======================================================================
    // 选中单位状态订阅（状态更新 → 重算移动/攻击高亮）
    // ======================================================================

    /// <summary>当前订阅了 OnUnitUpdate 的选中单位</summary>
    private Unit _subscribedUnit;

    private void SubscribeSelectedUnit(Unit unit)
    {
        if (unit == null || unit == _subscribedUnit) return;
        _subscribedUnit = unit;
        unit.OnUnitUpdate += OnSelectedUnitUpdated;
    }

    private void UnsubscribeSelectedUnit()
    {
        if (_subscribedUnit == null) return;
        _subscribedUnit.OnUnitUpdate -= OnSelectedUnitUpdated;
        _subscribedUnit = null;
    }

    /// <summary>选中单位状态更新（HP/位置/属性/buff 变化）→ 重算移动攻击高亮</summary>
    private void OnSelectedUnitUpdated()
    {
        if (_subscribedUnit == null) return;
        RecalculateRanges();
    }
}
