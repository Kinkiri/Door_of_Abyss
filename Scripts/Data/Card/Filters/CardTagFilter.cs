using Godot;
using Godot.Collections;

/// <summary>
/// 卡牌标签筛选：按模板标签过滤（任一匹配，与 TagTargetFilter 语义一致）。
/// </summary>
[GlobalClass]
public partial class CardTagFilter : CardFilter
{
    /// <summary>标签（任一匹配），空 = 不限制</summary>
    [Export] public Array<Tag> Tags { get; set; }

    public override bool IsMatch(Card card)
    {
        if (card == null) return false;
        if (Tags == null || Tags.Count == 0) return true;

        var cardTags = card.CardData?.Tags;
        if (cardTags == null) return false;
        foreach (var t in Tags)
            if (cardTags.Contains(t)) return true;
        return false;
    }
}
