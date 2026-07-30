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

    /// <summary>目标范围形状</summary>
    [Export] public virtual TargetShape Shape { get; set; } = TargetShape.None;

    /// <summary>目标阵营过滤</summary>
    [Export] public virtual TargetFilter Filter { get; set; } = TargetFilter.All;

    /// <summary>当 Shape 为 AreaDiamond/AreaSquare 时，扩散半径（曼哈顿距离或半径）</summary>
    [Export] public virtual int AreaRange { get; set; } = 0;

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

    /// <summary>打出条件，不满足时不出牌不扣费</summary>
    [Export] public Condition[] Conditions { get; set; }

    /// <summary>打出时执行的效果列表</summary>
    [Export] public GameAction[] Actions { get; set; }


    public override string ToString()
    {
        //输出所有数据
        return $"[Card: {CardID}] {CardName} - {Description} (Cost: {Cost}) | {Type} Shape={Shape} Filter={Filter}";
    }
}
