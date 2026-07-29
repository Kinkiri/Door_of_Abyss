using Godot;
using System;

/// <summary>
/// 单位卡牌数据类，继承自CardData，表示一种可以召唤单位的卡牌模板
/// </summary>
[GlobalClass]
public partial class UnitCardData : CardData
{
    [Export] public UnitData UnitData { get; set; }
}
