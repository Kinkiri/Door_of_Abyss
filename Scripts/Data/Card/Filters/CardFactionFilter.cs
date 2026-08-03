using Godot;

/// <summary>
/// 卡牌势力筛选：按 Faction 过滤。Faction.无 = 不限制（哨兵值）。
/// </summary>
[GlobalClass]
public partial class CardFactionFilter : CardFilter
{
    /// <summary>势力（无 = 不限制）</summary>
    [Export] public Faction Faction = Faction.无;

    public override bool IsMatch(Card card)
    {
        if (card == null) return false;
        if (Faction == Faction.无) return true;
        return card.CardData?.Faction == Faction;
    }
}
