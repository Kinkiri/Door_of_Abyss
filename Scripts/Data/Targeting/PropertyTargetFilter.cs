using Godot;
using System.Collections.Generic;

/// <summary>
/// 静态属性筛选的中间基类：复用"对候选逐个 IsUnitMatch 过滤 + 格子透传 + 无形状"的实现。
/// 具体维度（阵营/类型/标签/世界观/势力）由子类各自实现 IsUnitMatch。
/// </summary>
[GlobalClass]
public abstract partial class PropertyTargetFilter : TargetFilter
{
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
        => candidates ?? TargetResolver.AllCells(ctx.Map);

    public override TargetShape GetShape() => TargetShape.None;
}
