using Godot;
using System.Collections.Generic;

/// <summary>
/// NOT 组合筛选器：从候选集中排除内层子过滤器命中的目标（补集）。
/// 内层子过滤器以全量解析，再与传入候选求差集。
/// </summary>
[GlobalClass]
public partial class NotTargetFilter : TargetFilter
{
    /// <summary>被排除的子筛选器</summary>
    [Export] public TargetFilter Filter { get; set; }

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);
        if (Filter == null)
            return list;

        var excluded = new List<Unit>();
        var innerArr = Filter.ApplyUnits(null, ctx);
        if (innerArr != null) excluded.AddRange(innerArr);

        var result = new List<Unit>(list.Length);
        foreach (var u in list)
            if (u != null && !excluded.Contains(u))
                result.Add(u);
        return result.ToArray();
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllCells(ctx.Map);
        if (Filter == null)
            return list;

        var excluded = new List<Cell>();
        var innerArr = Filter.ApplyCells(null, ctx);
        if (innerArr != null) excluded.AddRange(innerArr);

        var result = new List<Cell>(list.Length);
        foreach (var c in list)
            if (c != null && !excluded.Contains(c))
                result.Add(c);
        return result.ToArray();
    }

    public override TargetShape GetShape() => Filter?.GetShape() ?? TargetShape.None;

    public override int GetAreaRange() => Filter?.GetAreaRange() ?? 1;

    public override CellShape GetCellShape() => Filter?.GetCellShape();

    public override TargetKind GetKind() => Filter?.GetKind() ?? TargetKind.Unit;

    public override TeamFilter GetTeamFilter()
    {
        // 补集语义：内层是敌方则结果是友方（UI 提示用，混合时返回 All）
        var t = Filter?.GetTeamFilter();
        return t switch
        {
            TeamFilter.Enemy => TeamFilter.Ally,
            TeamFilter.Ally => TeamFilter.Enemy,
            _ => TeamFilter.All,
        };
    }
}
