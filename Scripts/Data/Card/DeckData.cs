using Godot;

/// <summary>
/// 卡组数据，定义卡牌列表。
/// 在编辑器中新建 .tres Resource，拖入卡牌模板即可。
/// </summary>
[GlobalClass]
public partial class DeckData : Resource
{
    [Export] public string DeckName { get; set; } = "未命名卡组";
    /// <summary>卡组中的卡牌模板列表</summary>
    [Export] public CardData[] Cards { get; set; }
}
