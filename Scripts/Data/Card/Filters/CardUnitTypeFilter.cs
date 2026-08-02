using Godot;
using Godot.Collections;

/// <summary>
/// 单位卡单位类型筛选：按单位卡绑定的 UnitData.Type 过滤（任一匹配，与 UnitTypeTargetFilter 语义一致）。
/// 非单位卡（UnitData 为 null）不匹配——单位类型只在单位卡上有意义。
/// </summary>
[GlobalClass]
public partial class CardUnitTypeFilter : CardFilter
{
    /// <summary>单位类型（任一匹配），空 = 不限制</summary>
    [Export] public Array<UnitType> UnitTypes { get; set; }

    public override bool IsMatch(Card card)
    {
        if (card == null) return false;
        if (UnitTypes == null || UnitTypes.Count == 0) return true;

        var unitCard = card.CardData as UnitCardData;
        if (unitCard?.UnitData == null) return false;
        return UnitTypes.Contains(unitCard.UnitData.Type);
    }
}
