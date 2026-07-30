using Godot;
using System;

/// <summary>
/// 装备卡牌数据类，继承自CardData。
/// </summary>
public partial class EquipmentCardData : CardData
{
    [Export] public EquipmentData EquipmentData { get; set; }
}
