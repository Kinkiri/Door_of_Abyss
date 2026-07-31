using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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

        // 加载完成后校验数据
        ValidateAll();
    }

    /// <summary>
    /// 校验所有已加载卡牌的合法性，对不合法项输出 GD.PushWarning。
    /// 不抛异常——校验是辅助排查，不影响正常加载流程。
    /// </summary>
    public static void ValidateAll()
    {
        int errorCount = 0;
        var sb = new StringBuilder();
        var seenIDs = new HashSet<string>();

        foreach (var card in CardList)
        {
            // 1) CardID 不能为空/未配置
            if (string.IsNullOrWhiteSpace(card.CardID) || card.CardID == "UnknownCard")
            {
                sb.AppendLine($"  [{card.GetType().Name}] CardID 未配置或仍为默认值");
                errorCount++;
            }

            // 2) CardID 不能重复
            if (!string.IsNullOrWhiteSpace(card.CardID) && card.CardID != "UnknownCard")
            {
                if (!seenIDs.Add(card.CardID))
                {
                    sb.AppendLine($"  [CardID={card.CardID}] 重复 CardID");
                    errorCount++;
                }
            }

            // 3) UnitCardData → TargetShape 必须是 SingleCell
            if (card is UnitCardData unitCard && unitCard.Shape != TargetShape.SingleCell)
            {
                sb.AppendLine($"  [{card.CardID}] 单位卡的 Shape 应为 SingleCell，当前为 {unitCard.Shape}");
                errorCount++;
            }

            // 4) EquipmentCardData → 必须有 EquipmentData + Shape 必须 SingleUnit + EquipmentID 非空
            if (card is EquipmentCardData equipCard)
            {
                if (equipCard.EquipmentData == null)
                {
                    sb.AppendLine($"  [CardID={card.CardID}] 装备卡未配置 EquipmentData");
                    errorCount++;
                }
                else if (string.IsNullOrWhiteSpace(equipCard.EquipmentData.EquipmentID))
                {
                    sb.AppendLine($"  [CardID={card.CardID}] 装备的 EquipmentID 未配置");
                    errorCount++;
                }

                if (equipCard.Shape != TargetShape.SingleUnit)
                {
                    sb.AppendLine($"  [CardID={card.CardID}] 装备卡的 Shape 应为 SingleUnit，当前为 {equipCard.Shape}");
                    errorCount++;
                }
            }

            // 5) Cost 不能为负
            if (card.Cost < 0)
            {
                sb.AppendLine($"  [{card.CardID}] Cost={card.Cost}，不能为负");
                errorCount++;
            }

            // 6) AreaRange 不能为负
            if (card.AreaRange < 0)
            {
                sb.AppendLine($"  [{card.CardID}] AreaRange={card.AreaRange}，不能为负");
                errorCount++;
            }
        }

        if (errorCount > 0)
        {
            GD.PushWarning($"[CardLibrary] 校验发现在 {CardList.Count} 张卡牌中有 {errorCount} 个问题:\n{sb}");
        }
        else
        {
            GD.Print($"[CardLibrary] 校验完成：{CardList.Count} 张卡牌全部通过");
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
