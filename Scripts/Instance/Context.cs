using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 上下文类，用于传递游戏运行时的全局状态和数据
/// </summary>
public partial class Context
{
    /// <summary>战场地图（格子字典），由 Manager 层在创建/入队时填充，供 TargetResolver 使用</summary>
    public Dictionary<Vector2I, Cell> Map { get; set; }

    /// <summary>战场活跃单位列表，由 Manager 层在创建/入队时填充，供 TargetResolver 使用</summary>
    public List<Unit> ActiveUnits { get; set; }

    public Team SourceTeam { get; set; }
    public Team TargetTeam { get; set; }
    public Card SourceCard { get; set; }
    public Unit SourceUnit { get; set; }

    /// <summary>触发 OnUnitAct 时的行动类型（移动/攻击），供被动效果区分</summary>
    public UnitActType ActType { get; set; }

    /// <summary>本次伤害事件的修饰量（正=加伤，负=减伤）。OnBeforeDamage 被动用 ModifyDamageAction 增减，DamageAction 结算时应用</summary>
    public int DamageModifier { get; set; }

    /// <summary>
    /// 范围攻击再用
    /// </summary>
    public Unit[] TargetUnits { get; set; }
    public Unit TargetUnit { get; set; }
    public Cell SourceCell { get; set; }

    /// <summary>
    /// 范围攻击再用
    /// </summary>
    public Cell[] TargetCells { get; set; }
    public Cell TargetCell { get; set; }
}
