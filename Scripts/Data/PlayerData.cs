using Godot;

/// <summary>
/// 玩家全局数据，跨关卡持久化。
/// 在编辑器中新建 .tres Resource，配置玩家卡组、门数据等属性。
/// </summary>
[GlobalClass]
public partial class PlayerData : Resource
{
    /// <summary>玩家构筑的卡组</summary>
    [Export] public DeckData PlayerDeck { get; set; }

    /// <summary>玩家门（水晶）数据模板</summary>
    [Export] public UnitData DoorData { get; set; }
}
