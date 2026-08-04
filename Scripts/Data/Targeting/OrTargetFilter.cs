using Godot;
using System.Collections.Generic;

/// <summary>
/// OR 组合筛选器：任一子过滤器命中则保留该候选（对传入候选过滤）。
/// 子过滤器按"单候选解析"语义判定（形状节点会自生成候选集并检查是否包含该候选）。
/// </summary>
[GlobalClass]
public partial class OrTargetFilter : TargetFilter
{
    /// <summary>子筛选器（任一命中即保留）</summary>
    [Export] public TargetFilter[] Filters { get; set; }

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);
        if (Filters == null || Filters.Length == 0)
            return list;

        var result = new List<Unit>(list.Length);
        foreach (var u in list)
        {
            if (u == null) continue;
            foreach (var f in Filters)
            {
                if (f == null) continue;
                var hit = f.ApplyUnits(new[] { u }, ctx);
                if (hit != null && hit.Length > 0)
                {
                    result.Add(u);
                    break;
                }
            }
        }
        return result.ToArray();
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllCells(ctx.Map);
        if (Filters == null || Filters.Length == 0)
            return list;

        var result = new List<Cell>(list.Length);
        foreach (var c in list)
        {
            if (c == null) continue;
            foreach (var f in Filters)
            {
                if (f == null) continue;
                var hit = f.ApplyCells(new[] { c }, ctx);
                if (hit != null && hit.Length > 0)
                {
                    result.Add(c);
                    break;
                }
            }
        }
        return result.ToArray();
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

    public override CellShape GetCellShape()
    {
        if (Filters != null)
            foreach (var f in Filters)
            {
                var s = f?.GetCellShape();
                if (s != null) return s;
            }
        return null;
    }

    public override TargetKind GetKind()
    {
        if (Filters != null)
            foreach (var f in Filters)
                if (f != null && f.GetShape() != TargetShape.None)
                    return f.GetKind();
        return TargetKind.Unit;
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
}
