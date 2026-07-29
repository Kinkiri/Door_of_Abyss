using Godot;

/// <summary>
/// 门（水晶）数据模板，继承 UnitData 并附加门专属属性。
/// 未来支持多门时，每个门独立配置自己的部署范围。
/// </summary>
[GlobalClass]
public partial class DoorData : UnitData
{
    /// <summary>此门提供的单位部署范围（曼哈顿距离），默认 2</summary>
    [Export] public int DeployRange { get; set; } = 2;
}
