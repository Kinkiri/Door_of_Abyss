using Godot;
using System;

/// <summary>
/// 单位视觉实体，跟随 Unit 数据更新位置和属性显示。
/// 生命周期由 UnitManager 管理，不自行调用 Manager 逻辑。
/// 鼠标悬停显示描述面板，需要用户将 Sprite/Area2D 的 MouseEntered/MouseExited 信号连接到对应方法。
/// </summary>
public partial class UnitView : Node2D
{
    [Export] public UnitData UnitData { get; set; }
    public Unit Unit { get; set; }
    [Export] public Label NameLabel;
    [Export] public Label HPLabel;
    [Export] public Label ATKLabel;

    /// <summary>描述面板，鼠标悬停时显示</summary>
    [Export] public Panel DescriptionPanel { get; set; }

    /// <summary>描述面板内的文本</summary>
    [Export] public Label DescriptionLabel { get; set; }

    /// <summary>敌方标志精灵，UnitView 创建时自动判断显示/隐藏</summary>
    [Export] public ColorRect EnemyIndicator { get; set; }

    public override void _Ready()
    {
        if (Unit == null)
        {
            GD.PrintErr("UnitView: Unit 未赋值，销毁");
            QueueFree();
            return;
        }

        if (UnitData == null)
            UnitData = Unit.UnitData;

        // 敌方标记：只有敌方单位才显示
        if (EnemyIndicator != null)
            EnemyIndicator.Visible = Unit.Team == Team.Enemy;

        // 敌方单位名字红色
        if (NameLabel != null && Unit.Team == Team.Enemy)
            NameLabel.Modulate = Colors.Red;

        if (DescriptionPanel != null)
            DescriptionPanel.Hide();

        if (DescriptionLabel != null && UnitData != null)
            DescriptionLabel.Text = $"{UnitData.Description} \n" +
                                        $"HP: {UnitData.HealthPoints} " +
                                        $"ATK: {UnitData.AttackPower}\n" +
                                        $"AD: {UnitData.AttackDistance} " +
                                        $"AP: {UnitData.ActionPoints}";
        Unit.OnUnitUpdate += UpdateView;
        UpdateView();
    }

    public override void _ExitTree()
    {
        if (Unit != null)
            Unit.OnUnitUpdate -= UpdateView;
    }

    public override void _Process(double delta)
    {
        if (Unit == null || Unit.IsDead)
        {
            QueueFree();
            return;
        }
    }

    public void UpdateView()
    {
        if (UnitData == null) return;

        if (NameLabel != null)
            NameLabel.Text = UnitData.UnitName;
        if (HPLabel != null)
            HPLabel.Text = $" {Unit.CurrentHP}/{Unit.MaxHP}";
        if (ATKLabel != null)
            ATKLabel.Text = $" {Unit.AttackPower}";
        Position = MapManager.Instance.GridToWorld(Unit.GridPos);
    }

    /// <summary>鼠标悬停进入时调用（用户从 Sprite/Area2D 的 MouseEntered 信号连接）</summary>
    public void OnMouseEntered()
    {
        if (DescriptionPanel != null)
            DescriptionPanel.Show();
    }

    /// <summary>鼠标悬停离开时调用（用户从 Sprite/Area2D 的 MouseExited 信号连接）</summary>
    public void OnMouseExited()
    {
        if (DescriptionPanel != null)
            DescriptionPanel.Hide();
    }
}
