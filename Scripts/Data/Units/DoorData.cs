using Godot;

/// <summary>
/// 门（水晶）数据模板，继承 UnitData 并附加门专属属性。
/// 多门时每个门独立配置自己的经济收益。
/// </summary>
[GlobalClass]
public partial class DoorData : UnitData
{
    /// <summary>此门提供的单位部署范围（曼哈顿距离），默认 2</summary>
    [Export] public int DeployRange { get; set; } = 2;

    /// <summary>每回合此门回复的费用，默认 2</summary>
    [Export] public int CostPerRound { get; set; } = 2;

    /// <summary>每回合此门提供的抽牌数，默认 1</summary>
    [Export] public int DrawPerRound { get; set; } = 1;
}
