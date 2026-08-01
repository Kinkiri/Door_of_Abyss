using Godot;

/// <summary>
/// 世界观筛选器：按单位模板的世界观过滤候选单位（无 = 不限制）。
/// </summary>
[GlobalClass]
public partial class WorldTargetFilter : PropertyTargetFilter
{
    /// <summary>世界观过滤，无 = 不限制（来自 UnitData.World）</summary>
    [Export] public World World { get; set; } = World.无;

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;
        if (World == World.无) return true;
        return unit.UnitData?.World == World;
    }
}
