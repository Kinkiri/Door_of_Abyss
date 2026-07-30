using Godot;
using System;

/// <summary>
/// 上下文类，用于传递游戏运行时的全局状态和数据
/// </summary>
public partial class Context
{
    public Team SourceTeam { get; set; }
    public Team TargetTeam { get; set; }
    public Card SourceCard { get; set; }
    public Unit SourceUnit { get; set; }

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

    /// <summary>SummonUnitAction 召唤的单位，供 ViewAnimator 做入场动画</summary>
    public Unit SpawnedUnit { get; set; }

}
