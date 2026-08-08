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

    /// <summary>装备图标预制体，由用户在场景中创建并拖入</summary>
    [Export] public PackedScene EquipmentViewPrefab { get; set; }

    /// <summary>单位视图挂载层（BaseMapLayer）</summary>
    [Export] public Node UnitLayer { get; set; }

    /// <summary>Unit → UnitView 映射，用于移除时清理引用</summary>
    private readonly Dictionary<Unit, UnitView> _unitViews = new();

    /// <summary>Buff → BuffView 映射，用于移除时销毁视觉</summary>
    private readonly Dictionary<Buff, BuffView> _buffViews = new();

    /// <summary>Equipment → EquipmentView 映射，用于移除时销毁视觉</summary>
    private readonly Dictionary<Equipment, EquipmentView> _equipmentViews = new();

    public override void _Ready()
    {
        Instance = this;

        // 场景树顺序保证 Manager 节点先于本节点 _Ready（Manager 在 Map 之前）
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.OnUnitSpawned += OnUnitSpawned;
            UnitManager.Instance.OnUnitRemoved += OnUnitRemoved;
            UnitManager.Instance.OnUnitTransformed += OnUnitTransformed;
        }
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.BuffApplied += OnBuffApplied;
            BuffManager.Instance.BuffRemoved += OnBuffRemoved;
        }
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.EquipmentApplied += OnEquipmentApplied;
            EquipmentManager.Instance.EquipmentRemoved += OnEquipmentRemoved;
        }
    }

    public override void _ExitTree()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.OnUnitSpawned -= OnUnitSpawned;
            UnitManager.Instance.OnUnitRemoved -= OnUnitRemoved;
            UnitManager.Instance.OnUnitTransformed -= OnUnitTransformed;
        }
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.BuffApplied -= OnBuffApplied;
            BuffManager.Instance.BuffRemoved -= OnBuffRemoved;
        }
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.EquipmentApplied -= OnEquipmentApplied;
            EquipmentManager.Instance.EquipmentRemoved -= OnEquipmentRemoved;
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

        // 视图销毁时清理引用（死亡动画播完 QueueFree / 场景卸载均触发）——
        // 不在 OnUnitRemoved 立即清理：死亡动画期间引用保留，供镜头双点跟随（攻击致死后
        // UnitActed 回调需 GetUnitView(target) 建立攻击者+目标中点）与浮动数字锚点使用
        view.TreeExited += () => _unitViews.Remove(unit);

        // 敌方标志由 UnitView 的 EnemyIndicator 自己判断显示

        UnitLayer.AddChild(view);
        GD.Print($"[UnitViewManager] UnitView 创建: {unit.UnitData?.UnitName} ID={unit.ID}");
    }

    private void OnUnitRemoved(Unit unit)
    {
        // 不清理 _unitViews——引用由 UnitView.TreeExited 统一清理（见 OnUnitSpawned 注释）
        _ = unit;
    }

    private void OnUnitTransformed(Unit unit)
    {
        GD.Print($"[Transform][View] OnUnitTransformed 收到: unit={unit?.UnitData?.UnitName} " +
                 $"_unitViews命中={_unitViews.ContainsKey(unit)} _buffViews条目数={_buffViews.Count}");
        foreach (var kv in _buffViews)
            GD.Print($"[Transform][View]   _buffViews 条目: buff={kv.Key?.Data?.BuffID} view存在={kv.Value != null}");

        // 变身后刷新 UnitView 的模板引用与显示（名字/属性来自新 UnitData）
        if (_unitViews.TryGetValue(unit, out var view))
        {
            GD.Print($"[Transform][View] 命中 view，执行 RefreshUnitData + ClearUnitIcons");
            view.RefreshUnitData();
            ClearUnitIcons(view);
        }
    }

    /// <summary>清空单位视图下挂载的 Buff/装备图标（BuffView 挂 BuffContainer、装备挂 EquipmentContainer 或视图根）</summary>
    private void ClearUnitIcons(UnitView view)
    {
        var buffContainer = view.FindChild("BuffContainer", true, false);
        if (buffContainer != null)
        {
            foreach (var child in buffContainer.GetChildren())
            {
                if (child is BuffView bv)
                    _buffViews.Remove(bv.Buff);
                child.QueueFree();
            }
        }

        var equipContainer = view.FindChild("EquipmentContainer", true, false);
        if (equipContainer != null)
        {
            foreach (var child in equipContainer.GetChildren())
            {
                if (child is EquipmentView ev)
                    _equipmentViews.Remove(ev.Equipment);
                child.QueueFree();
            }
        }

        // 装备无容器时挂在视图根（固定位置避让 Buff 图标）
        foreach (var child in view.GetChildren())
        {
            if (child is EquipmentView ev)
            {
                _equipmentViews.Remove(ev.Equipment);
                child.QueueFree();
            }
        }
    }

    // ======================================================================
    // Buff 图标
    // ======================================================================

    private void OnBuffApplied(Unit target, Buff buff)
    {
        GD.Print($"[Transform][View] OnBuffApplied: target={target?.UnitData?.UnitName} buff={buff?.Data?.BuffID} " +
                 $"GetUnitView命中={GetUnitView(target) != null} BuffViewPrefab={BuffViewPrefab != null}");
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
        GD.Print($"[Transform][View] BuffView 已创建并注册: buff={buff?.Data?.BuffID} 挂载于 {container.Name}");
    }

    private void OnBuffRemoved(Unit target, Buff buff)
    {
        GD.Print($"[Transform][View] OnBuffRemoved: target={target?.UnitData?.UnitName} buff={buff?.Data?.BuffID} " +
                 $"_buffViews命中={_buffViews.ContainsKey(buff)}");
        if (_buffViews.TryGetValue(buff, out var bv))
        {
            bv.QueueFree();
            _buffViews.Remove(buff);
            GD.Print($"[Transform][View] BuffView 已 QueueFree: buff={buff?.Data?.BuffID}");
        }
    }

    // ======================================================================
    // 装备图标
    // ======================================================================

    private void OnEquipmentApplied(Unit target, Equipment equip)
    {
        if (EquipmentViewPrefab == null) return;
        var unitView = GetUnitView(target);
        if (unitView == null) return;

        var node = EquipmentViewPrefab.Instantiate<Node2D>();
        var ev = node as EquipmentView;
        if (ev == null)
        {
            GD.PrintErr("[UnitViewManager] EquipmentViewPrefab 根节点必须挂载 EquipmentView.cs 脚本");
            node.QueueFree();
            return;
        }

        ev.Setup(equip);

        // 优先挂到 UnitView 下名为 EquipmentContainer 的子节点（若场景配置了），
        // 否则挂 UnitView 根，固定位置避让 Buff 图标
        var container = unitView.FindChild("EquipmentContainer", true, false);
        if (container != null)
        {
            ev.Position = new Vector2(container.GetChildCount() * 30, 0);
            container.AddChild(ev);
        }
        else
        {
            ev.Position = new Vector2(0, -20);
            unitView.AddChild(ev);
        }

        _equipmentViews[equip] = ev;
    }

    private void OnEquipmentRemoved(Unit target, Equipment equip)
    {
        if (_equipmentViews.TryGetValue(equip, out var ev))
        {
            ev.QueueFree();
            _equipmentViews.Remove(equip);
        }
    }
}
