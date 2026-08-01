using Godot;

/// <summary>
/// 势力筛选器：按单位模板的势力过滤候选单位（无 = 不限制）。
/// </summary>
[GlobalClass]
public partial class FactionTargetFilter : PropertyTargetFilter
{
    /// <summary>势力过滤，无 = 不限制（来自 UnitData.Faction）</summary>
    [Export] public Faction Faction { get; set; } = Faction.无;

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;
        if (Faction == Faction.无) return true;
        return unit.UnitData?.Faction == Faction;
    }
}
