using Godot;
using System.Linq;

/// <summary>
/// 左下角单位/格子信息面板（View 层）。
/// 订阅 SelectionManager.SelectionUpdated 显示选中单位/格子的完整信息：
/// 描述、运行时属性、Buff、装备、脚下格子与环境。内容超出面板时由 ScrollContainer 滚动。
/// 挂在预制体根节点（PanelContainer），子节点全部用 [Export] 引用。
/// </summary>
public partial class UnitInfoPanel : PanelContainer
{
    [Export] public Label TitleLabel;
    [Export] public Label DescLabel;
    [Export] public Label StatsLabel;
    [Export] public Control CardUnitSection;
    [Export] public Label CardUnitLabel;
    [Export] public Control CardEnvSection;
    [Export] public Label CardEnvLabel;
    [Export] public Control CardEquipSection;
    [Export] public Label CardEquipLabel;
    [Export] public Control BuffSection;
    [Export] public Label BuffLabel;
    [Export] public Control EquipSection;
    [Export] public Label EquipLabel;
    [Export] public Control CellSection;
    [Export] public Label CellLabel;
    [Export] public Control EnvSection;
    [Export] public Label EnvLabel;

    /// <summary>当前正在展示的单位（用于订阅其属性变化刷新）</summary>
    private Unit _displayedUnit;

    public override void _Ready()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionUpdated += OnSelectionUpdated;
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.BuffApplied += OnBuffChanged;
            BuffManager.Instance.BuffRemoved += OnBuffChanged;
        }
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.EquipmentApplied += OnEquipmentChanged;
            EquipmentManager.Instance.EquipmentRemoved += OnEquipmentChanged;
        }
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentApplied += OnEnvironmentChanged;
            EnvironmentManager.Instance.EnvironmentRemoved += OnEnvironmentChanged;
        }
        if (UnitManager.Instance != null)
            UnitManager.Instance.OnUnitTransformed += OnUnitTransformed;

        Hide();
    }

    public override void _ExitTree()
    {
        SetDisplayedUnit(null);

        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionUpdated -= OnSelectionUpdated;
        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.BuffApplied -= OnBuffChanged;
            BuffManager.Instance.BuffRemoved -= OnBuffChanged;
        }
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.EquipmentApplied -= OnEquipmentChanged;
            EquipmentManager.Instance.EquipmentRemoved -= OnEquipmentChanged;
        }
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentApplied -= OnEnvironmentChanged;
            EnvironmentManager.Instance.EnvironmentRemoved -= OnEnvironmentChanged;
        }
        if (UnitManager.Instance != null)
            UnitManager.Instance.OnUnitTransformed -= OnUnitTransformed;
    }

    // ======================================================================
    // 刷新驱动
    // ======================================================================

    /// <summary>选中状态变化：单位 → 完整信息；空格子 → 格子+环境；无 → 隐藏</summary>
    private void OnSelectionUpdated()
    {
        OnRefresh();
    }

    private void OnBuffChanged(Unit unit, Buff buff) => OnRefresh();
    private void OnEquipmentChanged(Unit unit, Equipment equip) => OnRefresh();
    private void OnEnvironmentChanged(Cell cell, Environment env) => OnRefresh();
    private void OnUnitTransformed(Unit unit) => OnRefresh();

    /// <summary>按当前选中状态重绘面板</summary>
    private void OnRefresh()
    {
        var sel = SelectionManager.Instance;
        if (sel == null) { Hide(); return; }

        // 出牌模式优先（此时 SelectedUnit 可能仍保留之前选中）
        if (sel.SelectedCard != null) { ShowCard(sel.SelectedCard); return; }
        if (sel.SelectedUnit != null) { ShowUnit(sel.SelectedUnit); return; }
        if (sel.SelectedCell != null) { ShowCell(sel.SelectedCell); return; }

        SetDisplayedUnit(null);
        Hide();
    }

    // ======================================================================
    // 内容拼装
    // ======================================================================

    private void ShowUnit(Unit unit)
    {
        if (unit == null || unit.IsDead || !unit.IsAlive) { Hide(); return; }

        SetDisplayedUnit(unit);
        Visible = true;

        // 隐藏卡牌模式附加区（防止单位卡模板信息残留）
        CardUnitSection.Visible = false;
        CardEnvSection.Visible = false;
        CardEquipSection.Visible = false;

        var data = unit.UnitData;

        TitleLabel.Text = $"{data.UnitName}　{unit.Type}·{TeamName(unit.Team)}·{data.Rarity}";
        DescLabel.Text = data.Description;

        string tags = data.Tags != null && data.Tags.Count > 0
            ? string.Join(", ", data.Tags) : "无";
        StatsLabel.Text =
            $"HP：{unit.CurrentHP} / {unit.MaxHP}\n" +
            $"攻击力：{unit.AttackPower}\n" +
            $"体力：{unit.Stamina}\n" +
            $"攻击范围：{unit.AttackDistance}\n" +
            $"行动点：{unit.ActionPoints} / {unit.MaxActionPoints}\n" +
            $"本回合行动次数：{unit.ActionsThisTurn}\n" +
            $"ID：{data.UnitID}　世界：{data.World}　势力：{data.Faction}\n" +
            $"标签：{tags}";

        // Buff 列表
        var buffs = BuffManager.Instance?.GetBuffs(unit);
        if (buffs != null && buffs.Count > 0)
        {
            BuffSection.Visible = true;
            BuffLabel.Text = string.Join("\n\n", buffs.Select(b =>
                $"{b.Data.BuffName} ×{b.StackCount}（{TurnsText(b.RemainingTurns)}）\n{b.Data.Description}"));
        }
        else
        {
            BuffSection.Visible = false;
        }

        // 装备
        var equip = EquipmentManager.Instance?.GetEquipment(unit);
        if (equip != null)
        {
            EquipSection.Visible = true;
            EquipLabel.Text = $"{equip.Data.EquipmentName}：{equip.Data.Description}";
        }
        else
        {
            EquipSection.Visible = false;
        }

        // 脚下格子 + 环境
        if (MapManager.Instance != null && MapManager.Instance.TryGetCell(unit.GridPos, out Cell cell))
        {
            CellSection.Visible = true;
            CellLabel.Text = BuildCellText(cell);
            ShowEnvironment(cell.Environment);
        }
        else
        {
            CellSection.Visible = false;
            EnvSection.Visible = false;
        }
    }

    private void ShowCell(Cell cell)
    {
        if (cell == null) { Hide(); return; }

        SetDisplayedUnit(null);
        Visible = true;

        // 隐藏卡牌模式附加区（防止单位卡模板信息残留）
        CardUnitSection.Visible = false;
        CardEnvSection.Visible = false;
        CardEquipSection.Visible = false;

        var block = cell.BaseBlock;
        TitleLabel.Text = $"{block.BlockName}　({cell.GridPos.X}, {cell.GridPos.Y})";
        DescLabel.Text = block.BlockDescription;
        StatsLabel.Text =
            $"移动消耗：{cell.MoveCost}\n" +
            $"可站立：{(cell.CanStand ? "✓" : "✗")}　可穿越：{(cell.CanPass ? "✓" : "✗")}";

        BuffSection.Visible = false;
        EquipSection.Visible = false;
        CellSection.Visible = false;
        ShowEnvironment(cell.Environment);
    }

    private void ShowCard(Card card)
    {
        if (card == null) { Hide(); return; }

        SetDisplayedUnit(null);
        Visible = true;

        var data = card.CardData;

        TitleLabel.Text = $"{card.CardName}　{CardTypeName(card.Type)}·{data.Rarity}";
        DescLabel.Text = card.Description;

        string tags = data.Tags != null && data.Tags.Count > 0
            ? string.Join(", ", data.Tags) : "无";
        StatsLabel.Text =
            $"费用：{card.Cost}\n" +
            $"ID：{data.CardID}　世界：{data.World}　势力：{data.Faction}\n" +
            $"标签：{tags}\n" +
            $"目标：{TargetShapeText(card.TargetFilter)}";

        // 隐藏所有附加区，仅显示当前卡牌类型对应的区
        CardUnitSection.Visible = false;
        CardEnvSection.Visible = false;
        CardEquipSection.Visible = false;
        BuffSection.Visible = false;
        EquipSection.Visible = false;
        CellSection.Visible = false;
        EnvSection.Visible = false;

        // 单位卡：额外显示单位信息
        if (data is UnitCardData unitCard && unitCard.UnitData != null)
        {
            var ud = unitCard.UnitData;
            CardUnitSection.Visible = true;
            CardUnitLabel.Text =
                $"{ud.UnitName}　{ud.Type}·{ud.Rarity}\n" +
                $"HP：{ud.HealthPoints}　攻击力：{ud.AttackPower}　体力：{ud.Stamina}　射程：{ud.AttackDistance}　行动点：{ud.ActionPoints}\n" +
                $"{ud.Description}\n" +
                $"被动效果：{ud.PassiveEffects?.Length ?? 0} 个";
        }

        // 环境卡：额外显示环境信息
        if (data is EnvironmentCardData envCard && envCard.EnvironmentData != null)
        {
            var ed = envCard.EnvironmentData;
            CardEnvSection.Visible = true;
            CardEnvLabel.Text =
                $"{ed.EnvironmentName}　{DurationText(ed.Duration)}\n" +
                $"{ed.Description}\n" +
                $"移动消耗修正：{ed.MoveCostDelta}";
        }

        // 装备卡：额外显示装备信息
        if (data is EquipmentCardData equipCard && equipCard.EquipmentData != null)
        {
            var eqd = equipCard.EquipmentData;
            CardEquipSection.Visible = true;
            CardEquipLabel.Text =
                $"{eqd.EquipmentName}：{eqd.Description}\n" +
                $"加成：攻击+{eqd.AttackBonus} 生命+{eqd.MaxHealthBonus} 射程+{eqd.AttackDistanceBonus} " +
                $"体力+{eqd.StaminaBonus} 行动点+{eqd.ActionPointBonus}";
        }
    }

    private void ShowEnvironment(Environment env)
    {
        if (env?.Data == null)
        {
            EnvSection.Visible = false;
            return;
        }

        EnvSection.Visible = true;
        EnvLabel.Text =
            $"{env.Data.EnvironmentName}（{TurnsText(env.RemainingTurns)}）\n" +
            $"{env.Data.Description}\n" +
            $"移动消耗修正：{env.Data.MoveCostDelta}";
    }

    private static string BuildCellText(Cell cell)
    {
        var block = cell.BaseBlock;
        return $"{block.BlockName}　({cell.GridPos.X}, {cell.GridPos.Y})\n" +
               $"{block.BlockDescription}\n" +
               $"移动消耗：{cell.MoveCost}　可站立：{(cell.CanStand ? "✓" : "✗")}　可穿越：{(cell.CanPass ? "✓" : "✗")}";
    }

    private static string TurnsText(int turns) => turns < 0 ? "永久" : $"剩余 {turns} 回合";

    private static string DurationText(int duration) => duration < 0 ? "永久" : $"持续 {duration} 回合";

    private static string CardTypeName(CardType type) => type switch
    {
        CardType.Unit        => "单位卡",
        CardType.Spell       => "法术卡",
        CardType.Environment => "环境卡",
        CardType.Equipment   => "装备卡",
        _                    => "特殊卡",
    };

    private static string TargetShapeText(TargetFilter filter)
    {
        if (filter == null) return "无目标（直接打出）";
        return filter.GetShape() switch
        {
            TargetShape.None        => "无目标（直接打出）",
            TargetShape.All         => "全地图",
            TargetShape.SingleUnit  => "点选单位",
            TargetShape.SingleCell  => "点选格子",
            TargetShape.AreaDiamond => "菱形区域",
            TargetShape.AreaSquare  => "方形区域",
            _                       => filter.GetShape().ToString(),
        };
    }

    private static string TeamName(Team team) => team switch
    {
        Team.Player => "玩家方",
        Team.Enemy  => "敌方",
        _           => "中立",
    };

    // ======================================================================
    // 单位订阅管理
    // ======================================================================

    private void SetDisplayedUnit(Unit unit)
    {
        if (_displayedUnit == unit) return;
        if (_displayedUnit != null)
            _displayedUnit.OnUnitUpdate -= OnUnitUpdate;
        _displayedUnit = unit;
        if (unit != null)
            unit.OnUnitUpdate += OnUnitUpdate;
    }

    /// <summary>单位运行时属性变化（HP 等）时刷新面板</summary>
    private void OnUnitUpdate()
    {
        if (_displayedUnit == null) return;
        if (_displayedUnit.IsDead || !_displayedUnit.IsAlive) { Hide(); return; }
        OnRefresh();
    }
}
