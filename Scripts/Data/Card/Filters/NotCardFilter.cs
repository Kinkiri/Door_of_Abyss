using Godot;

/// <summary>
/// NOT 组合筛选器：内层子筛选器命中的排除（补集），如 Not[CardType(单位)] = 非单位牌。
/// </summary>
[GlobalClass]
public partial class NotCardFilter : CardFilter
{
    /// <summary>被排除的子筛选器</summary>
    [Export] public CardFilter Filter { get; set; }

    public override bool IsMatch(Card card)
    {
        if (card == null) return false;
        if (Filter == null) return true;
        return !Filter.IsMatch(card);
    }
}
