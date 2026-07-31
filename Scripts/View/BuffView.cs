using Godot;

/// <summary>
/// 单个 Buff 图标，由 UnitViewManager 在 BuffApplied/BuffRemoved 事件时创建/销毁。
/// 需要用户创建预制体并拖入 UnitViewManager.BuffViewPrefab，内含：
///   - TextureRect（图标）
///   - Label（数字，可选）
///   - Panel + Label（描述，鼠标悬停显示）
/// 然后挂好引用即可。
/// </summary>
public partial class BuffView : Node2D
{
    public Buff Buff { get; set; }

    [Export] public TextureRect IconRect { get; set; }
    [Export] public Label CountLabel { get; set; }
    [Export] public Panel DescriptionPanel { get; set; }
    [Export] public Label DescriptionLabel { get; set; }

    public void Setup(Buff buff)
    {
        Buff = buff;
        if (IconRect != null && buff?.Data?.Icon != null)
            IconRect.Texture = buff.Data.Icon;

        // 悬停显示 buff 描述
        if (DescriptionLabel != null && buff?.Data != null)
            DescriptionLabel.Text = $"{buff.Data.BuffName}: {buff.Data.Description}";
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

    public override void _Process(double delta)
    {
        if (Buff == null || CountLabel == null) return;

        if (Buff.StackCount > 1)
            CountLabel.Text = Buff.StackCount.ToString();
        else if (Buff.Data.Duration > 0 && Buff.RemainingTurns > 1)
            CountLabel.Text = Buff.RemainingTurns.ToString();
        else
            CountLabel.Text = "";
    }
}
