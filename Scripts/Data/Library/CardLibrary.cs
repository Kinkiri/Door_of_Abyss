using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 卡牌库类，负责加载和管理所有卡牌数据资源
/// </summary>
public partial class CardLibrary : Library
{
    /// <summary>卡牌数据资源路径</summary>
    const string CardDataPath = "res://Resource/Data/Cards/";

    /// <summary>卡牌列表，包含所有已加载的卡牌对象</summary>
    public static List<CardData> CardList { get; private set; } = new();

    /// <summary>卡牌字典，键为 CardID，值为卡牌对象</summary>
    public static Dictionary<string, CardData> CardDictionary { get; private set; } = new();

    static CardLibrary()
    {
        // 读取所有 CardData 资源文件
        CardList.AddRange(LoadResourcesFromPaths<CardData>(GetAllTresPaths(CardDataPath)));

        // 初始化卡牌字典
        foreach (var card in CardList)
        {
            if (!CardDictionary.ContainsKey(card.CardID))
            {
                CardDictionary.Add(card.CardID, card);
            }
            else
            {
                GD.PrintErr($"卡牌ID重复: {card.CardID}");
            }
        }

        // 输出已加载的卡牌信息
        GD.Print($"已加载 {CardList.Count} 张卡牌数据:");
        foreach (var card in CardList)
        {
            GD.Print($"  {card}");
        }
    }

    public static CardData GetCardByID(string cardID)
    {
        if (CardDictionary.TryGetValue(cardID, out var card))
        {
            return card;
        }
        else
        {
            GD.PrintErr($"未找到卡牌ID: {cardID}");
            return null;
        }
    }
}
