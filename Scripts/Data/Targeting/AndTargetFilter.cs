using Godot;
using System;

/// <summary>
/// AND 组合筛选器：Filters 按顺序应用——自动取第一个形状节点生成候选，
/// 其余节点（无形状的过滤/组合节点）依次过滤。顺序无关，重复形状节点被忽略。
/// </summary>
[GlobalClass]
public partial class AndTargetFilter : TargetFilter
{
    /// <summary>子筛选器（通常第一个为形状节点，其余为过滤节点）</summary>
    [Export] public TargetFilter[] Filters { get; set; }

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        if (Filters == null || Filters.Length == 0)
            return candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);

        // 取第一个形状节点生成候选；无形状节点时从全量开始
        TargetFilter shapeNode = FindShapeNode(Filters);
        Unit[] cur = shapeNode != null
            ? shapeNode.ApplyUnits(null, ctx)
            : candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);

        // 其余节点依次过滤（跳过形状节点与自身）
        foreach (var f in Filters)
        {
            if (f == null || f == shapeNode) continue;
            if (f.GetShape() != TargetShape.None) continue;
            cur = f.ApplyUnits(cur, ctx);
        }
        return cur ?? Array.Empty<Unit>();
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        if (Filters == null || Filters.Length == 0)
            return candidates ?? TargetResolver.AllCells(ctx.Map);

        TargetFilter shapeNode = FindShapeNode(Filters);
        Cell[] cur = shapeNode != null
            ? shapeNode.ApplyCells(null, ctx)
            : candidates ?? TargetResolver.AllCells(ctx.Map);

        foreach (var f in Filters)
        {
            if (f == null || f == shapeNode) continue;
            if (f.GetShape() != TargetShape.None) continue;
            cur = f.ApplyCells(cur, ctx);
        }
        return cur ?? Array.Empty<Cell>();
    }

    public override TargetShape GetShape()
    {
        if (Filters != null)
            foreach (var f in Filters)
                if (f != null && f.GetShape() != TargetShape.None)
                    return f.GetShape();
        return TargetShape.None;
    }

    public override int GetAreaRange()
    {
        if (Filters != null)
            foreach (var f in Filters)
                if (f != null && f.GetShape() != TargetShape.None)
                    return f.GetAreaRange();
        return 1;
    }

    public override TargetKind GetKind()
    {
        var shapeNode = FindShapeNode(Filters);
        return shapeNode?.GetKind() ?? TargetKind.Unit;
    }

    public override TeamFilter GetTeamFilter()
    {
        if (Filters != null)
            foreach (var f in Filters)
            {
                var t = f?.GetTeamFilter();
                if (t != null && t != TeamFilter.All) return t.Value;
            }
        return TeamFilter.All;
    }

    private static TargetFilter FindShapeNode(TargetFilter[] filters)
    {
        foreach (var f in filters)
            if (f != null && f.GetShape() != TargetShape.None)
                return f;
        return null;
    }
}
