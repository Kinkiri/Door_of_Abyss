using Godot;
using System.Collections.Generic;

/// <summary>
/// 动态条件筛选器：按 Conditions（配合 ValueSource 可筛运行时属性，如 HP≤50%MaxHP）过滤候选。
/// 对单位候选构造 ctx.TargetUnit=候选；对格子候选构造 ctx.TargetCell=候选。
/// </summary>
[GlobalClass]
public partial class ConditionTargetFilter : TargetFilter
{
    /// <summary>附加条件（AND 关系），对每个候选单独判定</summary>
    [Export] public Condition[] Conditions { get; set; }

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);
        var result = new List<Unit>(list.Length);
        foreach (var u in list)
            if (IsUnitMatch(u, ctx.SourceTeam, ctx))
                result.Add(u);
        return result.ToArray();
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllCells(ctx.Map);
        var result = new List<Cell>(list.Length);
        foreach (var c in list)
            if (IsCellMatch(c, ctx))
                result.Add(c);
        return result.ToArray();
    }

    public override TargetShape GetShape() => TargetShape.None;

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;
        if (Conditions == null || Conditions.Length == 0) return true;

        var sub = new Context
        {
            SourceUnit = ctx.SourceUnit,
            TargetUnit = unit,
            TargetTeam = unit.Team,
            SourceTeam = ctx.SourceTeam,
            SourceCard = ctx.SourceCard,
            Map = ctx.Map,
            ActiveUnits = ctx.ActiveUnits,
        };
        foreach (var c in Conditions)
            if (c != null && !c.IsMet(sub)) return false;
        return true;
    }

    public override bool IsCellMatch(Cell cell, Context ctx)
    {
        if (cell == null) return false;
        if (Conditions == null || Conditions.Length == 0) return true;

        var sub = new Context
        {
            SourceUnit = ctx.SourceUnit,
            TargetCell = cell,
            SourceTeam = ctx.SourceTeam,
            SourceCard = ctx.SourceCard,
            Map = ctx.Map,
            ActiveUnits = ctx.ActiveUnits,
        };
        foreach (var c in Conditions)
            if (c != null && !c.IsMet(sub)) return false;
        return true;
    }
}
