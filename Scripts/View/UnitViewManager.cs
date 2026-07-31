using Godot;
using System.Collections.Generic;

/// <summary>
/// 单位/Buff 视图管理器（View 层）。
/// 订阅 UnitManager / BuffManager 的事件，负责 UnitView 与 BuffView 的创建、挂载和销毁。
/// 事件驱动模式，与 CardManager → HandPanel 一致：Manager 只发事件，不碰视图。
/// 需添加到场景中（Level.tscn 的 Map 节点下），并配置 UnitLayer 与 BuffViewPrefab。
/// </summary>
public partial class UnitViewManager : Node
{
    public static UnitViewManager Instance { get; private set; }

    /// <summary>Buff 图标预制体，由用户在场景中创建并拖入</summary>
    [Export] public PackedScene BuffViewPrefab { get; set; }

    /// <summary>单位视图挂载层（BaseMapLayer）</summary>
    [Export] public Node UnitLayer { get; set; }

    /// <summary>Unit → UnitView 映射，用于移除时清理引用</summary>
    private readonly Dictionary<Unit, UnitView> _unitViews = new();

    /// <summary>Buff → BuffView 映射，用于移除时销毁视觉</summary>
    private readonly Dictionary<Buff, BuffView> _buffViews = new();

    public override void _Ready()
    {
        Instance = this;

        // 场景树顺序保证 Manager 节点先于本节点 _Ready（Manager 在 Map 之前）
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.OnUnitSpawned += OnUnitSpawned;
            UnitManager.Instance.OnUnitRemoved += OnUnitRemoved;
        }
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.BuffApplied += OnBuffApplied;
            BuffManager.Instance.BuffRemoved += OnBuffRemoved;
        }
    }

    public override void _ExitTree()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.OnUnitSpawned -= OnUnitSpawned;
            UnitManager.Instance.OnUnitRemoved -= OnUnitRemoved;
        }
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.BuffApplied -= OnBuffApplied;
            BuffManager.Instance.BuffRemoved -= OnBuffRemoved;
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>获取单位的视觉节点</summary>
    public UnitView GetUnitView(Unit unit)
    {
        _unitViews.TryGetValue(unit, out var view);
        return view;
    }

    // ======================================================================
    // 单位视图
    // ======================================================================

    private void OnUnitSpawned(Unit unit)
    {
        if (unit?.UnitData?.UnitPrefab == null) return;
        if (UnitLayer == null) return;

        var view = unit.UnitData.UnitPrefab.Instantiate<UnitView>();
        view.UnitData = unit.UnitData;
        view.Unit = unit;
        view.Position = MapManager.Instance.GridToWorld(unit.GridPos);

        _unitViews[unit] = view;

        // 敌方标志由 UnitView 的 EnemyIndicator 自己判断显示

        UnitLayer.AddChild(view);
        GD.Print($"[UnitViewManager] UnitView 创建: {unit.UnitData?.UnitName} ID={unit.ID}");
    }

    private void OnUnitRemoved(Unit unit)
    {
        // 清理视觉节点引用（不 QueueFree——UnitView 自己播完死亡动画后销毁）
        if (_unitViews.TryGetValue(unit, out var view))
            _unitViews.Remove(unit);
    }

    // ======================================================================
    // Buff 图标
    // ======================================================================

    private void OnBuffApplied(Unit target, Buff buff)
    {
        if (BuffViewPrefab == null) return;
        var unitView = GetUnitView(target);
        if (unitView == null) return;

        // 挂到 UnitView 下名为 BuffContainer 的子节点上（HBoxContainer）
        var container = unitView.FindChild("BuffContainer", true, false);
        if (container == null)
        {
            GD.Print("[UnitViewManager] UnitView 下未找到 BuffContainer，跳过图标创建");
            return;
        }

        var node = BuffViewPrefab.Instantiate<Node2D>();
        var bv = node as BuffView;
        if (bv == null)
        {
            GD.PrintErr("[UnitViewManager] BuffViewPrefab 根节点必须挂载 BuffView.cs 脚本");
            node.QueueFree();
            return;
        }

        bv.Setup(buff);
        // Node2D 子节点不被 HBoxContainer 自动布局，手动设置位置避免重叠
        bv.Position = new Vector2(container.GetChildCount() * 30, 0);
        container.AddChild(bv);
        _buffViews[buff] = bv;
    }

    private void OnBuffRemoved(Unit target, Buff buff)
    {
        if (_buffViews.TryGetValue(buff, out var bv))
        {
            bv.QueueFree();
            _buffViews.Remove(buff);
        }
    }
}
