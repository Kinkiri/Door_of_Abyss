using Godot;

/// <summary>
/// 关卡配置数据
/// </summary>
[GlobalClass]
public partial class LevelData : Resource
{
    [Export] public string LevelName { get; set; } = "未命名关卡";

    [Export] public string Description { get; set; } = "";

    [Export] public WaveData[] Waves { get; set; }

    [Export] public MapData MapData { get; set; }

    /// <summary>玩家门放置区域（含）</summary>
    [Export] public Vector2I DoorPlaceZoneMin { get; set; }

    /// <summary>玩家门放置区域（含）</summary>
    [Export] public Vector2I DoorPlaceZoneMax { get; set; }

    /// <summary>默认刷怪区域最小坐标（含），WaveData 不配区域时用这个</summary>
    [Export] public Vector2I DefaultSpawnAreaMin { get; set; }

    /// <summary>默认刷怪区域最大坐标（含），WaveData 不配区域时用这个</summary>
    [Export] public Vector2I DefaultSpawnAreaMax { get; set; }

    /// <summary>关卡固定卡组，不为空时覆盖玩家卡组</summary>
    [Export] public DeckData LevelDeck { get; set; }
}
