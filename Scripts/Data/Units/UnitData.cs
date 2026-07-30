using Godot;

/// <summary>
/// 单位数据类，定义战斗中一个单位（角色/敌人）的基础属性模板
/// </summary>
[GlobalClass]
public partial class UnitData : Resource
{
    [Export] public string UnitID { get; set; } = "UnknownUnit";
    /// <summary>单位名称</summary>
    [Export] public string UnitName { get; set; } = "未知单位";

    /// <summary>攻击力</summary>
    [Export] public int AttackPower { get; set; } = 1;

    /// <summary>生命值上限</summary>
    [Export] public int HealthPoints { get; set; } = 2;

    /// <summary>体力上限（曼哈顿距离）</summary>
    [Export] public int Stamina { get; set; } = 1;

    /// <summary>攻击范围（曼哈顿距离）</summary>
    [Export] public int AttackDistance { get; set; } = 1;

    /// <summary>单位类型</summary>
    [Export] public UnitType Type { get; set; } = UnitType.Squad;

    /// <summary>行动点数</summary>
    [Export] public int ActionPoints { get; set; } = 1;

    /// <summary>单位描述信息</summary>
    [Export] public string Description { get; set; } = "暂无描述";

    /// <summary>单位预制体资源，用于在战场上实例化运行时单位对象</summary>
    [Export] public PackedScene UnitPrefab { get; set; }

    /// <summary>被动效果列表</summary>
    [Export] public EffectData[] PassiveEffects { get; set; }

    public override string ToString()
    {
        // 输出中文描述信息
        return $"[UnitData {UnitID}] {UnitName} | " +
               $"攻击力={AttackPower} 生命值={HealthPoints} 体力={Stamina} 攻击范围={AttackDistance} 类型={Type} 动作点数={ActionPoints}";
    }
}
