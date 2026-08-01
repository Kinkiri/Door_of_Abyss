using Godot;
using Godot.Collections;

/// <summary>
/// 卡牌模板数据
/// </summary>
[GlobalClass]
public abstract partial class CardData : Resource
{
    [Export] public string CardID { get; set; } = "UnknownCard";

    [Export] public string CardName { get; set; } = "未命名卡牌";

    [Export] public string Description { get; set; } = "暂无描述";

    [Export] public virtual CardType Type { get; set; } = CardType.Special;

    

    /// <summary>世界观</summary>
    [Export] public World World { get; set; } = World.测试;

    /// <summary>势力</summary>
    [Export] public Faction Faction { get; set; } = Faction.测试;

    /// <summary>普通标签</summary>
    [Export] public Array<Tag> Tags { get; set; }

    /// <summary>稀有度</summary>
    [Export] public Rarity Rarity { get; set; } = Rarity.Basic;

    /// <summary>使用消耗（行动次数 / 法力值）</summary>
    [Export] public int Cost { get; set; } = 1;

    [Export] public Texture2D Icon { get; set; }

    /// <summary>目标筛选器数组（默认 And 组合；null/空 = 无目标，直接打出）。替代旧 Shape + Filter + AreaRange 三个字段</summary>
    [Export] public TargetFilter[] TargetFilters { get; set; }

    /// <summary>打出条件，不满足时不出牌不扣费</summary>
    [Export] public Condition[] Conditions { get; set; }

    /// <summary>打出时执行的效果列表</summary>
    [Export] public GameAction[] Actions { get; set; }


    public override string ToString()
    {
        //输出所有数据
        return $"[Card: {CardID}] {CardName} - {Description} (Cost: {Cost}) | {Type} Target={string.Join("+", System.Array.ConvertAll(TargetFilters ?? System.Array.Empty<TargetFilter>(), f => f?.GetType().Name ?? "null"))}";
    }
}
