using Godot;
using Godot.Collections;

/// <summary>
/// 标签筛选器：按标签过滤候选单位（任一匹配，与 ModifyStatAction.RequiredTags 语义一致）。
/// </summary>
[GlobalClass]
public partial class TagTargetFilter : PropertyTargetFilter
{
    /// <summary>标签过滤（任一匹配），空 = 不限制</summary>
    [Export] public Array<Tag> Tags { get; set; }

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;
        if (Tags == null || Tags.Count == 0) return true;

        var unitTags = unit.UnitData?.Tags;
        if (unitTags == null) return false;
        foreach (var t in Tags)
            if (unitTags.Contains(t)) return true;
        return false;
    }
}
