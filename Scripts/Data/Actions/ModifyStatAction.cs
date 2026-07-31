using Godot;
using Godot.Collections;

/// <summary>
/// 修改单位战斗属性（可逆，Buff 到期自动还原）。
/// 修改固定数值而非百分比，叠层时效果线性叠加。
/// MaxHP 语义：施加时当前生命随上限同步增加；还原时上限减回、当前生命不随减（超出截断）。
/// RequiredTags：仅当目标单位带任一这些 Tag 时才生效（null/空 = 不限制）。
/// Tag 来自 UnitData 模板（战斗中不变），故 Apply/Revert 条件必然对称，可逆安全。
/// </summary>
[GlobalClass]
public partial class ModifyStatAction : GameAction
{
    [Export] public ModifyStatType TargetStat;
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    /// <summary>仅当目标单位带任一这些 Tag 时生效；null/空 = 不限制</summary>
    [Export] public Array<Tag> RequiredTags { get; set; }

    private bool TagRequiredMet(Context ctx)
    {
        if (RequiredTags == null || RequiredTags.Count == 0) return true;
        var unitTags = ctx.TargetUnit?.UnitData?.Tags;
        if (unitTags == null) return false;
        foreach (var tag in RequiredTags)
            if (unitTags.Contains(tag)) return true;
        return false;
    }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null) return;
        if (!TagRequiredMet(ctx)) return;

        int val = ValueSource?.GetValue(ctx) ?? Value;

        switch (TargetStat)
        {
            case ModifyStatType.AttackPower:
                ctx.TargetUnit.AttackPower += val;
                break;
            case ModifyStatType.MaxHP:
                ctx.TargetUnit.MaxHP += val;
                // 施加时当前生命随上限同步增加相同值（只增不减）
                ctx.TargetUnit.CurrentHP += val;
                break;
            case ModifyStatType.Stamina:
                ctx.TargetUnit.Stamina += val;
                break;
            case ModifyStatType.AttackDistance:
                ctx.TargetUnit.AttackDistance += val;
                break;
            case ModifyStatType.ActionPoints:
                // 改上限（最小 1），当前行动点随上限同步增加（参照 MaxHP 语义）
                ctx.TargetUnit.MaxActionPoints += val;
                ctx.TargetUnit.ActionPoints += val;
                break;
        }

        ctx.TargetUnit.UpdateUnit();
        GD.Print($"[ModifyStatAction] {TargetStat} {val:+0;-0} → {ctx.TargetUnit.UnitData?.UnitName}");
    }

    public override void Revert(Context ctx)
    {
        if (ctx?.TargetUnit == null) return;
        if (!TagRequiredMet(ctx)) return;

        int val = ValueSource?.GetValue(ctx) ?? Value;

        switch (TargetStat)
        {
            case ModifyStatType.AttackPower:
                ctx.TargetUnit.AttackPower -= val;
                break;
            case ModifyStatType.MaxHP:
                ctx.TargetUnit.MaxHP -= val;
                // 当前生命不随上限减少，仅超出新上限时截断
                ctx.TargetUnit.CurrentHP = Mathf.Min(ctx.TargetUnit.CurrentHP, ctx.TargetUnit.MaxHP);
                break;
            case ModifyStatType.Stamina:
                ctx.TargetUnit.Stamina -= val;
                break;
            case ModifyStatType.AttackDistance:
                ctx.TargetUnit.AttackDistance -= val;
                break;
            case ModifyStatType.ActionPoints:
                // 上限减回（最小不低于 1），当前行动点不随上限减少，仅超出新上限时截断
                ctx.TargetUnit.MaxActionPoints = System.Math.Max(1, ctx.TargetUnit.MaxActionPoints - val);
                ctx.TargetUnit.ActionPoints = System.Math.Min(ctx.TargetUnit.ActionPoints, ctx.TargetUnit.MaxActionPoints);
                break;
        }

        ctx.TargetUnit.UpdateUnit();
        GD.Print($"[ModifyStatAction] 还原 {TargetStat} {val:+0;-0} → {ctx.TargetUnit.UnitData?.UnitName}");
    }
}
