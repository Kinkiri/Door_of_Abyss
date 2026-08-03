using Godot;

/// <summary>
/// 卡牌费用筛选：按 Cost 区间过滤。负值 = 该端不限制。
/// </summary>
[GlobalClass]
public partial class CardCostFilter : CardFilter
{
    /// <summary>最小费用（含），-1 = 不限制</summary>
    [Export] public int MinCost = -1;

    /// <summary>最大费用（含），-1 = 不限制</summary>
    [Export] public int MaxCost = -1;

    public override bool IsMatch(Card card)
    {
        if (card == null) return false;
        if (MinCost >= 0 && card.Cost < MinCost) return false;
        if (MaxCost >= 0 && card.Cost > MaxCost) return false;
        return true;
    }
}
