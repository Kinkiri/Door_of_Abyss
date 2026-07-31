using Godot;
using System;
/// <summary>
/// 装备数据类，继承自Resource，表示一种可以装备在单位上的装备模板
/// 一个单位只能装备一个装备，装备后会增加单位的属性值和附加被动效果。
/// 属性加成可逆：移除装备时按相同数值减回（MaxHP 还原时截断当前生命）。
/// </summary>
[GlobalClass]
public partial class EquipmentData : Resource
{
    [Export] public string EquipmentID { get; set; }
    [Export] public string EquipmentName { get; set; }
    [Export] public string Description { get; set; }

    /// <summary>装备图标，供 EquipmentView 显示（与 BuffData.Icon 对齐）</summary>
    [Export] public Texture2D Icon { get; set; }

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

    /// <summary>
    /// 装备附加动作（仿 BuffData.OnApplyActions）。装备时 Execute、移除时 Revert。
    /// 与五个 bonus 字段叠加：装备时先执行非 0 bonus 转换的 ModifyStatAction，再执行此数组。
    /// </summary>
    [Export] public GameAction[] OnApplyActions { get; set; }
}
