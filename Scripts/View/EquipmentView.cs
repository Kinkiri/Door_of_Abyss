using Godot;

/// <summary>
/// 单个装备图标，由 UnitViewManager 在 EquipmentApplied/EquipmentRemoved 事件时创建/销毁。
/// 需要用户创建预制体并拖入 UnitViewManager.EquipmentViewPrefab，内含：
///   - TextureRect（图标）
///   - Panel + Label（描述，鼠标悬停显示）
/// 然后挂好引用即可。
/// </summary>
public partial class EquipmentView : Node2D
{
    public Equipment Equipment { get; set; }

    [Export] public TextureRect IconRect { get; set; }
    [Export] public Panel DescriptionPanel { get; set; }
    [Export] public Label DescriptionLabel { get; set; }

    public void Setup(Equipment equipment)
    {
        Equipment = equipment;
        if (IconRect != null && equipment?.Data?.Icon != null)
            IconRect.Texture = equipment.Data.Icon;

        // 悬停显示装备描述
        if (DescriptionLabel != null && equipment?.Data != null)
            DescriptionLabel.Text = $"{equipment.Data.EquipmentName}: {equipment.Data.Description}";
        if (DescriptionPanel != null)
            DescriptionPanel.Hide();
    }

    public override void _Ready()
    {
        if (IconRect != null)
        {
            IconRect.MouseEntered += OnIconMouseEntered;
            IconRect.MouseExited += OnIconMouseExited;
        }
    }

    public override void _ExitTree()
    {
        if (IconRect != null)
        {
            IconRect.MouseEntered -= OnIconMouseEntered;
            IconRect.MouseExited -= OnIconMouseExited;
        }
    }

    private void OnIconMouseEntered()
    {
        if (DescriptionPanel != null)
            DescriptionPanel.Show();
    }

    private void OnIconMouseExited()
    {
        if (DescriptionPanel != null)
            DescriptionPanel.Hide();
    }
}
