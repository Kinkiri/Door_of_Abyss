using Godot;
using System.Collections.Generic;

/// <summary>
/// 玩家卡组存档（user://deck.cfg）：配置面板写入、战斗加载读取。
/// 纯静态 IO，不引用管理器；卡组以 CardID 字符串列表存储（允许重复，单卡上限由 UI 约束），
/// 读取时经 CardLibrary 解析回模板，无效 ID 静默跳过。
/// 无存档 / 文件不存在 → 空列表（调用方自行兜底）。
/// </summary>
public static class PlayerDeckSave
{
    /// <summary>玩家卡组存档路径（与 settings.cfg 同目录，独立文件）</summary>
    private const string DeckCfgPath = "user://deck.cfg";

    /// <summary>读取玩家配置的卡牌模板列表</summary>
    public static List<CardData> LoadCards()
    {
        var result = new List<CardData>();
        var cfg = new ConfigFile();
        if (cfg.Load(DeckCfgPath) != Error.Ok) return result;

        var ids = cfg.GetValue("deck", "cards", new Godot.Collections.Array<string>())
            .As<Godot.Collections.Array>();
        if (ids == null) return result;

        foreach (var id in ids)
        {
            string s = id.AsString();
            if (s.Length > 0 && CardLibrary.CardDictionary.TryGetValue(s, out var card))
                result.Add(card);
        }
        return result;
    }

    /// <summary>实时写盘玩家配置卡组</summary>
    public static void SaveCards(IEnumerable<CardData> cards)
    {
        var ids = new Godot.Collections.Array<string>();
        if (cards != null)
        {
            foreach (var card in cards)
            {
                if (card != null) ids.Add(card.CardID);
            }
        }

        var cfg = new ConfigFile();
        cfg.SetValue("deck", "cards", ids);
        cfg.Save(DeckCfgPath);
    }
}
