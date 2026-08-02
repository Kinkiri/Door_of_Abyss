using Godot;

/// <summary>卡牌信息取值类型（可数值化的卡牌属性）</summary>
public enum CardInfoType
{
    /// <summary>费用</summary>
    Cost,

    /// <summary>卡牌类型（枚举数值，配合 CompareCondition 做类型判断）</summary>
    Type,

    /// <summary>世界观（枚举数值）</summary>
    World,

    /// <summary>势力（枚举数值）</summary>
    Faction,

    /// <summary>稀有度（枚举数值）</summary>
    Rarity,
}

/// <summary>
/// 卡牌信息值源，从 Context.SourceCard 读取卡牌属性（卡牌被动/打出场景 = 被抽/被打出的牌）。
/// 与 BuffInfoValue 同模式：Info 指定取值类型，找不到时返回 DefaultValue。
/// </summary>
[GlobalClass]
public partial class CardInfoValue : ValueSource
{
    [Export] public CardInfoType Info { get; set; } = CardInfoType.Cost;

    /// <summary>SourceCard 为 null 时的默认返回值</summary>
    [Export] public int DefaultValue { get; set; } = 0;

    public override int GetValue(Context ctx)
    {
        var cardData = ctx.SourceCard?.CardData;
        if (cardData == null) return DefaultValue;

        return Info switch
        {
            CardInfoType.Cost => cardData.Cost,
            CardInfoType.Type => (int)cardData.Type,
            CardInfoType.World => (int)cardData.World,
            CardInfoType.Faction => (int)cardData.Faction,
            CardInfoType.Rarity => (int)cardData.Rarity,
            _ => DefaultValue,
        };
    }
}
