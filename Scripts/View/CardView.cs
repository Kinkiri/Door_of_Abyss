using Godot;

/// <summary>
/// 卡牌视觉实体，展示卡牌信息，悬停效果由 HandPanel 统一控制
/// </summary>
public partial class CardView : TextureButton
{
    public Card Card { get; set; }

    [Export] public Label CardName;
    [Export] public Label CardCost;
    [Export] public Label CardDescription;
    [Export] public Panel DescriptionPanel;

    public override void _Ready()
    {
        if (Card == null)
        {
            GD.PrintErr("CardView: Card 未赋值");
            return;
        }

        if (DescriptionPanel != null)
            DescriptionPanel.Visible = false;

        MouseEntered += () => ShowDescription(true);
        MouseExited += () => ShowDescription(false);

        UpdateView();
    }

    public void UpdateView()
    {
        if (Card == null) return;

        if (CardName != null)
            CardName.Text = Card.CardName;
        if (CardCost != null)
            CardCost.Text = $"{Card.Cost}";
        if (CardDescription != null)
            CardDescription.Text = Card.Description;
        if (Card.CardData is UnitCardData unitCard)
        {
            if (unitCard.UnitData != null)
            {
                CardDescription.Text += $"\n" +
                                        $"{unitCard.UnitData.Description} " +
                                        $"HP: {unitCard.UnitData.HealthPoints} " +
                                        $"ATK: {unitCard.UnitData.AttackPower}\n" +
                                        $"AD: {unitCard.UnitData.AttackDistance} " +
                                        $"AP: {unitCard.UnitData.ActionPoints}";
            }
        }
    }

    private void ShowDescription(bool visible)
    {
        if (DescriptionPanel == null) return;
        DescriptionPanel.Visible = visible;
    }
}
