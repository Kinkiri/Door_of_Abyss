using Godot;
using Godot.Collections;

/// <summary>
/// 卡牌类型筛选：按 CardType 过滤（任一匹配，与 TagTargetFilter 语义一致）。
/// </summary>
[GlobalClass]
public partial class CardTypeFilter : CardFilter
{
    /// <summary>卡牌类型（任一匹配），空 = 不限制</summary>
    [Export] public Array<CardType> Types { get; set; }

    public override bool IsMatch(Card card)
    {
        if (card == null) return false;
        if (Types == null || Types.Count == 0) return true;
        foreach (var t in Types)
            if (card.Type == t) return true;
        return false;
    }
}
