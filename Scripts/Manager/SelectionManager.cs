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

    public void Init() { }

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

    /// <summary>由 HandPanel 调用：选中卡牌，进入瞄准模式</summary>
    public void OnCardClicked(Card card)
    {
        GD.Print($"[Selection] OnCardClicked: phase={BattleManager.Instance.CurrentPhase} team={BattleManager.Instance.CurrentTeam}");

        if (BattleManager.Instance.CurrentTeam != Team.Player)
        {
            GD.Print("[Selection] 非玩家行动阶段，不能出牌");
            return;
        }
        if (card.Cost > BattleManager.Instance.PlayerCost)
        {
            GD.Print($"[Selection] 费用不足：需要 {card.Cost}，当前 {BattleManager.Instance.PlayerCost}");
            return;
        }
        SelectedCard = card;
        IsAimingMode = true;
        GD.Print($"[Selection] 选中卡牌: [{card.CardID}] {card.CardName}，请选择目标");
    }

    /// <summary>由 BattleManager 调用：行为执行完后刷新范围显示</summary>
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

        if (SelectedUnit.ActionPoints <= 0)
        {
            _reachable = null;
            _attackable = null;
            _attackRange = null;
            EmitSignal(SignalName.SelectionUpdated);
            return;
        }

        _reachable = PathFinder.GetReachableCellsWithAttackTargets(
            SelectedUnit.GridPos, SelectedUnit.RemainingStamina,
            SelectedUnit.AttackDistance, SelectedUnit.Team,
            MapManager.Instance.Map, out _attackable);
        _attackRange = CalcAttackRange(SelectedUnit);
        EmitSignal(SignalName.SelectionUpdated);
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

            if (!ValidateCardTarget(SelectedCard, ctx))
            {
                GD.Print($"[Selection] 目标无效，请在合法目标上点击");
                return;
            }

            GD.Print($"[Selection] 出牌: [{SelectedCard.CardID}] {SelectedCard.CardName} 目标={clickGrid}");
            CardPlayRequest?.Invoke(SelectedCard, ctx);
            ClearSelection();
            return;
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
        var shape = card.Shape;

        // 无目标 / 全地图 → 直接通过
        if (shape == TargetShape.None || shape == TargetShape.All)
            return true;

        // 需要选择格子
        if (shape is TargetShape.SingleCell or TargetShape.AreaDiamond or TargetShape.AreaSquare)
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
            }
            return true;
        }

        // 需要选择单位
        if (shape == TargetShape.SingleUnit)
        {
            if (ctx.TargetUnit == null)
            {
                GD.Print("[Selection] 需要选择一个单位");
                return false;
            }
            if (card.Filter == TargetFilter.Enemy && ctx.TargetUnit.Team == BattleManager.Instance.CurrentTeam)
            {
                GD.Print("[Selection] 需要选择一个敌方单位");
                return false;
            }
            if (card.Filter == TargetFilter.Ally && ctx.TargetUnit.Team != BattleManager.Instance.CurrentTeam)
            {
                GD.Print("[Selection] 需要选择一个友方单位");
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
        if (BattleManager.Instance.CurrentTeam == Team.Neutral)
            return;

        // 已选 + 可攻击 → 请求攻击
        if (SelectedUnit != null && _attackable?.Contains(gridPos) == true)
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
        if (SelectedUnit != null)
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
        SelectedCell = cell;
        GD.Print($"[Selection] 选中格子 ({gridPos.X}, {gridPos.Y})");
    }

    // ======================================================================
    // 选中 + 范围计算
    // ======================================================================

    private void SelectUnit(Unit unit)
    {
        SelectedUnit = unit;
        SelectedCell = null;
        MapManager.Instance.TryGetCell(unit.GridPos, out Cell cell);
        SelectedCell = cell;

        GD.Print($"[Selection] 选中单位 [ID={unit.ID}] {unit.UnitData?.UnitName}");

        if (unit.ActionPoints <= 0)
        {
            _reachable = null; _attackable = null; _attackRange = null;
            EmitSignal(SignalName.SelectionUpdated);
            return;
        }

        _reachable = PathFinder.GetReachableCellsWithAttackTargets(
            unit.GridPos, unit.RemainingStamina, unit.AttackDistance,
            unit.Team, MapManager.Instance.Map, out _attackable);
        _attackRange = CalcAttackRange(unit);
        EmitSignal(SignalName.SelectionUpdated);
    }

    /// <summary>计算攻击范围（排除友方格子）</summary>
    private HashSet<Vector2I> CalcAttackRange(Unit unit)
    {
        var raw = PathFinder.GetCellsInRange(unit.GridPos, unit.AttackDistance, MapManager.Instance.Map);
        raw.RemoveWhere(pos =>
            MapManager.Instance.TryGetCell(pos, out Cell c)
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

    /// <summary>根据卡牌 Shape + AreaRange 计算预览格子集合，过滤无效目标</summary>
    private static HashSet<Vector2I> ComputeCardPreview(Vector2I center, Card card)
    {
        var shape = card.Shape;
        if (shape == TargetShape.None)
            return null;

        var map = MapManager.Instance?.Map;
        if (map == null) return null;

        // 全地图目标：显示所有格子
        if (shape == TargetShape.All)
            return new HashSet<Vector2I>(map.Keys);

        if (!map.ContainsKey(center)) return null;

        // 点选单位：只显示有合法单位的位置
        if (shape == TargetShape.SingleUnit)
        {
            var cell = map[center];
            var unit = cell.OccupyingUnit;
            if (unit == null || !unit.IsAlive || unit.IsDead)
                return null;
            if (!IsTargetFilterMatch(card.Filter, unit))
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

        // 范围形状：显示所有在范围中的格子（区域本身即是目标）
        int range = card.CardData?.AreaRange ?? 1;
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

    /// <summary>检查格子是否在己方门的部署范围内</summary>
    private static bool IsWithinDeployRange(Vector2I gridPos)
    {
        var door = UnitManager.Instance?.PlayerDoor;
        if (door == null) return true;
        int range = (door.UnitData as DoorData)?.DeployRange ?? 2;
        int dist = System.Math.Abs(gridPos.X - door.GridPos.X) +
                   System.Math.Abs(gridPos.Y - door.GridPos.Y);
        return dist <= range;
    }

    private static bool IsTargetFilterMatch(TargetFilter filter, Unit target)
    {
        var currentTeam = BattleManager.Instance.CurrentTeam;
        return filter switch
        {
            TargetFilter.Enemy => target.Team != currentTeam,
            TargetFilter.Ally => target.Team == currentTeam,
            _ => true,
        };
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
        SelectedUnit = null;
        SelectedCell = null;
        SelectedCard = null;
        IsAimingMode = false;
        LastCardPreviewCells = null;
        _reachable = null;
        _attackable = null;
        _attackRange = null;
        EmitSignal(SignalName.SelectionUpdated);
    }
}
