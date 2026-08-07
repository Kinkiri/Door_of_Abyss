using Godot;
using System.Collections.Generic;

/// <summary>
/// 浮动数字层（View 层管理器，世界空间挂载层）。
/// 订阅 UnitManager 的伤害/治疗事件，实例化浮动数字预制体并挂载到本层。
/// 事件驱动模式，与 UnitViewManager / AudioManager 一致：Manager 只发事件，不碰视图。
/// 需添加到场景中（Level.tscn 的 Map 节点下），并配置 FloatingNumberPrefab。
/// </summary>
public partial class FloatingNumberLayer : Node2D
{
    public static FloatingNumberLayer Instance { get; private set; }

    /// <summary>浮动数字预制体，由用户在场景中创建并拖入</summary>
    [Export] public PackedScene FloatingNumberPrefab { get; set; }

    [Export] public Color DamageColor = new Color(1, 0.2f, 0.2f);
    [Export] public Color HealColor = new Color(0.2f, 1, 0.2f);

    /// <summary>锚点 → 当前活跃数字数（错开序号分配用），Finished 时释放</summary>
    private readonly Dictionary<UnitView, int> _activeCounts = new();

    public override void _Ready()
    {
        Instance = this;

        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.OnUnitDamaged += OnUnitDamaged;
            UnitManager.Instance.OnUnitHealed += OnUnitHealed;
        }
    }

    public override void _ExitTree()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.OnUnitDamaged -= OnUnitDamaged;
            UnitManager.Instance.OnUnitHealed -= OnUnitHealed;
        }
        if (Instance == this) Instance = null;
    }

    private void OnUnitDamaged(Unit unit, int amount)
    {
        ShowNumber(unit, $"-{amount}", DamageColor, amount);
    }

    private void OnUnitHealed(Unit unit, int amount)
    {
        ShowNumber(unit, $"+{amount}", HealColor, amount);
    }

    /// <summary>
    /// 创建浮动数字。事件由 UnitManager 发出时单位必有效（DamageUnit 在 DestroyUnit 前发事件，
    /// 致死伤害同样显示）；null 防御。无视图/未配置预制体时静默跳过（View 层缺配置不破坏逻辑层）。
    /// </summary>
    private void ShowNumber(Unit unit, string text, Color color, int amount)
    {
        if (unit == null) return;
        if (FloatingNumberPrefab == null) return;
        var anchor = UnitViewManager.Instance?.GetUnitView(unit);
        if (anchor == null) return;

        _activeCounts.TryGetValue(anchor, out int count);
        _activeCounts[anchor] = count + 1;

        var node = FloatingNumberPrefab.Instantiate<FloatingNumber>();
        node.Setup(anchor, text, color, count, amount);
        node.Finished += n => ReleaseSlot(anchor);
        AddChild(node);
    }

    private void ReleaseSlot(UnitView anchor)
    {
        if (!_activeCounts.TryGetValue(anchor, out int count)) return;
        count--;
        if (count <= 0) _activeCounts.Remove(anchor);
        else _activeCounts[anchor] = count;
    }
}
