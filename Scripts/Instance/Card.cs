using Godot;
using System;

/// <summary>
/// 运行时卡牌实例，包含卡牌的可变状态
/// </summary>
public partial class Card
{
    /// <summary>运行时唯一标识符，由 CardManager 分配</summary>
    public int ID { get; set; }

    /// <summary>卡牌模板引用</summary>
    public CardData CardData { get; set; }

    /// <summary>模板中的唯一标识（便捷访问）</summary>
    public string CardID => CardData?.CardID ?? "Unknown";

    /// <summary>卡牌名称</summary>
    public string CardName { get; set; }

    /// <summary>卡牌描述</summary>
    public string Description { get; set; }

    /// <summary>使用消耗</summary>
    public int Cost { get; set; }

    /// <summary>卡牌类型</summary>
    public CardType Type { get; set; }

    /// <summary>目标范围形状</summary>
    public TargetShape Shape { get; set; }

    /// <summary>目标阵营过滤</summary>
    public TargetFilter Filter { get; set; }

    public Card() { }

    public Card(CardData cardData)
    {
        CardData = cardData;
        InitializeFromData();
    }

    /// <summary>从模板拷贝属性到运行时字段</summary>
    public void InitializeFromData()
    {
        if (CardData == null) return;
        CardName = CardData.CardName;
        Description = CardData.Description;
        Cost = CardData.Cost;
        Type = CardData.Type;
        Shape = CardData.Shape;
        Filter = CardData.Filter;
    }
}
