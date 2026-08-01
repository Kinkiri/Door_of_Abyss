using Godot;
using Godot.Collections;

/// <summary>
/// 单位 ID 筛选器：按 UnitData.UnitID 过滤候选单位（任一匹配）。
/// "对某单位不生效" 用 Not 组合表达：Not[UnitIDTargetFilter{UnitIDs=[X]}]。
/// </summary>
[GlobalClass]
public partial class UnitIDTargetFilter : PropertyTargetFilter
{
    /// <summary>单位 ID 过滤（任一匹配，来自 UnitData.UnitID），空 = 不限制</summary>
    [Export] public Array<string> UnitIDs { get; set; }

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;
        if (UnitIDs == null || UnitIDs.Count == 0) return true;
        return UnitIDs.Contains(unit.UnitData?.UnitID);
    }
}
