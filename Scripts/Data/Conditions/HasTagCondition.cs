using Godot;
using Godot.Collections;

/// <summary>
/// Tag 条件：检查来源或目标单位是否带任一指定 Tag（任一匹配）。
/// Tag 来自 UnitData.Tags 模板，战斗中不变。
/// </summary>
[GlobalClass]
public partial class HasTagCondition : Condition
{
    [Export(PropertyHint.Enum, "来源,目标")] public ConditionTarget CheckTarget { get; set; } = ConditionTarget.Source;

    /// <summary>任一匹配即可</summary>
    [Export] public Array<Tag> Tags { get; set; }

    /// <summary>true=必须带，false=必须不带</summary>
    [Export] public bool Has { get; set; } = true;

    public override bool IsMet(Context ctx)
    {
        var unit = CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null || unit.UnitData == null) return false;

        bool hasTag = false;
        if (unit.UnitData.Tags != null && Tags != null)
        {
            foreach (var tag in Tags)
            {
                if (unit.UnitData.Tags.Contains(tag))
                {
                    hasTag = true;
                    break;
                }
            }
        }
        return hasTag == Has;
    }
}
