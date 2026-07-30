using Godot;
using System;
/// <summary>
/// 装备数据类，继承自Resource，表示一种可以装备在单位上的装备模板
/// 装备是永久加成，不可逆，一个单位只能装备一个装备，装备后会增加单位的属性值和附加被动效果
/// </summary>
public partial class EquipmentData : Resource
{
    [Export] public string EquipmentID { get; set; } 
    [Export] public string EquipmentName { get; set; }
    [Export]public string Description{ get; set; }

    /// <summary>
    /// 攻击加成，表示装备后单位的攻击力增加的数值
    /// </summary>
    [Export] public int AttackBonus { get; set; } = 1;
    /// <summary>
    /// 生命上限加成，表示装备后单位的生命值上限增加的数值
    /// </summary>
    [Export] public int MaxHealthBonus { get; set; } = 1;
    /// <summary>
    /// 攻击距离加成，表示装备后单位的攻击距离增加的数值
    /// </summary>
    [Export] public int AttackDistanceBonus { get; set; } = 0;
    /// <summary>
    /// 耐力加成，表示装备后单位的耐力值增加的数值
    /// </summary>
    [Export] public int StaminaBonus { get; set; } = 0;
    /// <summary>
    /// 行动点加成，表示装备后单位的行动点数增加的数值
    /// </summary>
    [Export] public int ActionPointBonus { get; set; } = 0;
    /// <summary>
    /// 装备附加的被动效果列表
    /// </summary>
    [Export] public EffectData[] PassiveEffects { get; set; }
}
