using Godot;
using System;

/// <summary>
/// 形状筛选器：候选集生成器（唯一的形状节点）。
/// 双通道：
/// - CustomShape（CellShape 多态类，推荐）：配置后由形状类生成候选格（十字/叉/射线/三角形/菱形/方形统一入口），
///   解析与预览共用同一套格子生成算法；CenterOverride 仍生效；Kind 由本节点显式指定。
/// - 旧枚举路径（Shape + AreaRange/AreaRangeValueSource）：仅兼容存量 .tres 资源，新配置请用 CustomShape。
/// 生成时排除死亡单位；不在此处做阵营/类型/标签过滤（由 Attribute/Condition 节点负责）。
/// </summary>
[GlobalClass]
public partial class ShapeTargetFilter : TargetFilter
{
    /// <summary>
    /// 自定义形状（CellShape 多态类）：非空时以此生成候选格（忽略 Shape 枚举路径）。
    /// 形状内格子生成见各形状类的语义（含中心格、经地图过滤越界）。
    /// </summary>
    [Export] public CellShape CustomShape { get; set; }

    /// <summary>范围形状（旧枚举路径，仅 CustomShape=null 时生效；兼容存量资源）</summary>
    [Export] public TargetShape Shape { get; set; } = TargetShape.None;

    /// <summary>AreaDiamond/AreaSquare 的扩散半径（曼哈顿距离）</summary>
    [Export] public int AreaRange { get; set; } = 1;

    /// <summary>替换扩散半径的值来源（旧枚举路径）</summary>
    [Export] public ValueSource AreaRangeValueSource { get; set; }

    /// <summary>
    /// 扩散中心覆盖：配置后以该坐标为中心（代替 ctx.TargetCell）。
    /// 被动路径（单位自身格/环境格/事件格）与卡牌路径（点击格）均被覆盖；null = 默认行为。
    /// 坐标无效/格子不存在时返回空结果。
    /// </summary>
    [Export] public CellValueSource CenterOverride { get; set; }

    /// <summary>结果集类型：单位或格子（Area/All 形状时需显式指定；SingleUnit 隐含 Unit、SingleCell 隐含 Cell）</summary>
    [Export] public TargetKind Kind { get; set; } = TargetKind.Unit;

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        if (CustomShape != null)
            return TargetResolver.UnitsFromCells(CustomShape.GetCells(ResolveCenter(ctx), ctx));

        AreaRange = AreaRangeValueSource?.GetValue(ctx) ?? AreaRange;
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
                return TargetResolver.UnitsInArea(ResolveCenter(ctx), AreaRange, diamond: true, ctx.Map);

            case TargetShape.AreaSquare:
                return TargetResolver.UnitsInArea(ResolveCenter(ctx), AreaRange, diamond: false, ctx.Map);

            case TargetShape.All:
                return TargetResolver.AllAliveUnits(ctx.ActiveUnits);

            default:
                return Array.Empty<Unit>();
        }
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        if (CustomShape != null)
            return CustomShape.GetCells(ResolveCenter(ctx), ctx);

        switch (Shape)
        {
            case TargetShape.None:
            case TargetShape.SingleUnit:
                return Array.Empty<Cell>();

            case TargetShape.SingleCell:
            {
                var center = ResolveCenter(ctx);
                return center != null
                    ? new[] { center }
                    : Array.Empty<Cell>();
            }

            case TargetShape.AreaDiamond:
                return TargetResolver.CellsInArea(ResolveCenter(ctx), AreaRange, diamond: true, ctx.Map);

            case TargetShape.AreaSquare:
                return TargetResolver.CellsInArea(ResolveCenter(ctx), AreaRange, diamond: false, ctx.Map);

            case TargetShape.All:
                return TargetResolver.AllCells(ctx.Map);

            default:
                return Array.Empty<Cell>();
        }
    }

    /// <summary>
    /// 解析中心格子：CenterOverride 指定坐标（经地图查格）优先，其次 ctx.TargetCell。
    /// 覆盖坐标无效/格子不存在时返回 null（上层得到空结果）。
    /// </summary>
    private Cell ResolveCenter(Context ctx)
    {
        if (CenterOverride != null)
        {
            var pos = CenterOverride.GetCell(ctx);
            if (pos == null) return null;
            var map = ctx.Map ?? MapManager.Instance?.Map;
            if (map != null && map.TryGetValue(pos.Value, out Cell c) && c != null)
                return c;
            return null;
        }
        return ctx.TargetCell;
    }

    public override TargetShape GetShape() => CustomShape?.GetCategory() ?? Shape;

    public override int GetAreaRange() => CustomShape != null ? CustomShape.GetAreaRange() : AreaRange;

    public override CellShape GetCellShape() => CustomShape;

    public override TargetKind GetKind() => Kind;
}
