using Godot;

/// <summary>
/// 波次数据，定义某一回合在指定区域生成的敌方单位
/// </summary>
[GlobalClass]
public partial class WaveData : Resource
{
    /// <summary>在第几回合生成</summary>
    [Export] public int Round { get; set; } = 1;

    /// <summary>生成区域最小坐标（含）</summary>
    [Export] public Vector2I SpawnAreaMin { get; set; }

    /// <summary>生成区域最大坐标（含）</summary>
    [Export] public Vector2I SpawnAreaMax { get; set; }

    /// <summary>要生成的单位数据列表</summary>
    [Export] public UnitData[] UnitDatas { get; set; }

}
