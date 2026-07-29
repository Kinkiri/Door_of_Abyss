using Godot;

/// <summary>
/// 卡牌模板数据
/// </summary>
[GlobalClass]
public abstract partial class CardData : Resource
{
    [Export] public string CardID { get; set; } = "UnknownCard";

    [Export] public string CardName { get; set; } = "未命名卡牌";

    [Export] public string Description { get; set; } = "暂无描述";

    [Export] public CardType Type { get; set; }

    /// <summary>目标范围形状</summary>
    [Export] public TargetShape Shape { get; set; }

    /// <summary>目标阵营过滤</summary>
    [Export] public TargetFilter Filter { get; set; } = TargetFilter.All;

    /// <summary>当 Shape 为 AreaDiamond/AreaSquare 时，扩散半径（曼哈顿距离）</summary>
    [Export] public int AreaRange { get; set; } = 1;

    /// <summary>使用消耗（行动次数 / 法力值）</summary>
    [Export] public int Cost { get; set; } = 1;

    [Export] public Texture2D Icon { get; set; }

    /// <summary>打出时执行的效果列表</summary>
    [Export] public GameAction[] Actions { get; set; }

    /// <summary>打出条件，不满足时不出牌不扣费</summary>
    [Export] public Condition[] Conditions { get; set; }

    public override string ToString()
    {
        //输出所有数据
        return $"[Card: {CardID}] {CardName} - {Description} (Cost: {Cost}) | {Type} Shape={Shape} Filter={Filter}";
    }
}
