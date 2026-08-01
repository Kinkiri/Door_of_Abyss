using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 极值筛选器：对上游候选按 Value（值源，按候选 Target 读取）排序，取最高/最低的 Count 个。
/// 候选数量不足 Count 时全部保留。后处理节点（GetShape=None，不生成候选）。
/// 例："治疗生命值最低的 3 个友方单位" → [Shape(全体), Team(友方), Extreme(生命值, 最低, 3)]
/// </summary>
[GlobalClass]
public partial class ExtremeTargetFilter : TargetFilter
{
    /// <summary>比较值（按候选 Target 读取，如 UnitStatValue(CurrentHP)）</summary>
    [Export] public ValueSource Value { get; set; }

    /// <summary>极值方向：最低 / 最高</summary>
    [Export] public ExtremeMode Mode { get; set; } = ExtremeMode.Lowest;

    /// <summary>保留数量；候选不足时全部保留</summary>
    [Export] public int Count { get; set; } = 1;

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);
        if (Count <= 0) return Array.Empty<Unit>();
        if (list.Length <= Count) return list;

        // 每个候选读值（构造子 ctx，TargetUnit=候选）
        var scored = new List<(Unit unit, int value)>(list.Length);
        foreach (var u in list)
        {
            if (!TargetResolver.IsValidTarget(u)) continue;
            var sub = new Context
            {
                SourceUnit = ctx.SourceUnit,
                TargetUnit = u,
                TargetTeam = u.Team,
                SourceTeam = ctx.SourceTeam,
                SourceCard = ctx.SourceCard,
                Map = ctx.Map,
                ActiveUnits = ctx.ActiveUnits,
            };
            int v = Value?.GetValue(sub) ?? 0;
            scored.Add((u, v));
        }

        // 稳定排序：平局保持上游顺序
        scored.Sort((a, b) => Mode == ExtremeMode.Lowest ? a.value.CompareTo(b.value) : b.value.CompareTo(a.value));

        var result = new Unit[Mathf.Min(Count, scored.Count)];
        for (int i = 0; i < result.Length; i++)
            result[i] = scored[i].unit;
        return result;
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
        => candidates ?? TargetResolver.AllCells(ctx.Map);

    public override TargetShape GetShape() => TargetShape.None;
}
