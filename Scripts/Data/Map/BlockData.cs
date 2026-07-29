using Godot;
using System;

/// <summary>
/// 地块数据类，表示地图中每个单元格的基础地形信息
/// </summary>
[GlobalClass]
public partial class BlockData : Resource
{
    /// <summary>
    /// 地块名称
    /// </summary>
    [Export] public string BlockName { get; set; } = "地板";

    /// <summary>
    /// 地块描述
    /// </summary>
    [Export] public string BlockDescription { get; set; } = "什么效果都没有";

    /// <summary>
    /// 移动消耗的体力
    /// </summary>
    [Export] public int MoveCost { get; set; } = 1;

    /// <summary>
    /// 是否可站立（容纳单位），默认 true
    /// </summary>
    [Export] public bool CanStand { get; set; } = true;

    /// <summary>
    /// 是否可通过，默认 true
    /// 就算这个地块不能站人，只要体力够跨过，就可以通过
    /// </summary>
    [Export] public bool CanPass { get; set; } = true;
}
