using Godot;
using Godot.Collections;

/// <summary>
/// 单位类型筛选器：按单位类型过滤候选单位。
/// </summary>
[GlobalClass]
public partial class UnitTypeTargetFilter : PropertyTargetFilter
{
    /// <summary>单位类型过滤，空 = 不限制</summary>
    [Export] public Array<UnitType> UnitTypes { get; set; }

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;
        if (UnitTypes == null || UnitTypes.Count == 0) return true;
        return UnitTypes.Contains(unit.Type);
    }
}
