using Godot;
using System;

/// <summary>
/// 形状筛选器：候选集生成器（唯一的形状节点）。
/// 根据 Shape + AreaRange 生成单位或格子候选，忽略传入的 candidates。
/// 生成时排除死亡单位；不在此处做阵营/类型/标签过滤（由 Attribute/Condition 节点负责）。
/// </summary>
[GlobalClass]
public partial class ShapeTargetFilter : TargetFilter
{
    /// <summary>范围形状</summary>
    [Export] public TargetShape Shape { get; set; } = TargetShape.None;

    /// <summary>AreaDiamond/AreaSquare 的扩散半径（曼哈顿距离）</summary>
    [Export] public int AreaRange { get; set; } = 1;

    /// <summary>结果集类型：单位或格子（Area/All 形状时需显式指定；SingleUnit 隐含 Unit、SingleCell 隐含 Cell）</summary>
    [Export] public TargetKind Kind { get; set; } = TargetKind.Unit;

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        switch (Shape)
        {
            case TargetShape.None:
            case TargetShape.SingleCell:
                return Array.Empty<Unit>();

            case TargetShape.SingleUnit:
                return TargetResolver.IsValidTarget(ctx.TargetUnit)
                    ? new[] { ctx.TargetUnit }
                    : Array.Empty<Unit>();

            case TargetShape.AreaDiamond:
                return TargetResolver.UnitsInArea(ctx.TargetCell, AreaRange, diamond: true, ctx.Map);

            case TargetShape.AreaSquare:
                return TargetResolver.UnitsInArea(ctx.TargetCell, AreaRange, diamond: false, ctx.Map);

            case TargetShape.All:
                return TargetResolver.AllAliveUnits(ctx.ActiveUnits);

            default:
                return Array.Empty<Unit>();
        }
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        switch (Shape)
        {
            case TargetShape.None:
            case TargetShape.SingleUnit:
                return Array.Empty<Cell>();

            case TargetShape.SingleCell:
                return ctx.TargetCell != null
                    ? new[] { ctx.TargetCell }
                    : Array.Empty<Cell>();

            case TargetShape.AreaDiamond:
                return TargetResolver.CellsInArea(ctx.TargetCell, AreaRange, diamond: true, ctx.Map);

            case TargetShape.AreaSquare:
                return TargetResolver.CellsInArea(ctx.TargetCell, AreaRange, diamond: false, ctx.Map);

            case TargetShape.All:
                return TargetResolver.AllCells(ctx.Map);

            default:
                return Array.Empty<Cell>();
        }
    }

    public override TargetShape GetShape() => Shape;

    public override int GetAreaRange() => AreaRange;

    public override TargetKind GetKind() => Kind;
}
