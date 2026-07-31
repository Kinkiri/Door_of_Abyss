using Godot;
using Godot.Collections;

/// <summary>
/// 修改单位战斗属性（可逆，Buff 到期自动还原）。
/// 支持多目标（遍历 ctx.TargetUnits），每个目标独立判定 Tag 和计算值。
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

    private bool TagRequiredMet(Unit unit)
    {
        if (RequiredTags == null || RequiredTags.Count == 0) return true;
        var unitTags = unit?.UnitData?.Tags;
        if (unitTags == null) return false;
        foreach (var tag in RequiredTags)
            if (unitTags.Contains(tag)) return true;
        return false;
    }

    // ------------------- 修改为多目标版本 -------------------
    protected override void Apply(Context ctx)
    {
        var originalTarget = ctx.TargetUnit;
        var units = (ctx.TargetUnits != null && ctx.TargetUnits.Length > 0)
                    ? ctx.TargetUnits
                    : new[] { ctx.TargetUnit };

        foreach (var unit in units)
        {
            if (unit == null || unit.IsDead) continue;

            // 临时将当前目标设为 Context 的主目标，使 ValueSource 能正确读取该目标属性
            ctx.TargetUnit = unit;

            // 检查 Tag 条件
            if (!TagRequiredMet(unit)) continue;

            int val = ValueSource?.GetValue(ctx) ?? Value;

            // 根据类型修改属性
            switch (TargetStat)
            {
                case ModifyStatType.AttackPower:
                    unit.AttackPower += val;
                    break;
                case ModifyStatType.MaxHP:
                    unit.MaxHP += val;
                    unit.CurrentHP += val;  // 施加时当前生命随上限增加
                    break;
                case ModifyStatType.Stamina:
                    unit.Stamina += val;
                    break;
                case ModifyStatType.AttackDistance:
                    unit.AttackDistance += val;
                    break;
                case ModifyStatType.ActionPoints:
                    unit.MaxActionPoints += val;
                    unit.ActionPoints += val;
                    break;
            }

            unit.UpdateUnit();
            GD.Print($"[ModifyStatAction] {TargetStat} {val:+0;-0} → {unit.UnitData?.UnitName}");
        }

        // 恢复原始 TargetUnit（防止后续使用混乱）
        ctx.TargetUnit = originalTarget;
    }

    public override void Revert(Context ctx)
    {
        var originalTarget = ctx.TargetUnit;
        var units = (ctx.TargetUnits != null && ctx.TargetUnits.Length > 0)
                    ? ctx.TargetUnits
                    : new[] { ctx.TargetUnit };

        foreach (var unit in units)
        {
            if (unit == null || unit.IsDead) continue;

            ctx.TargetUnit = unit;

            if (!TagRequiredMet(unit)) continue;

            int val = ValueSource?.GetValue(ctx) ?? Value;

            switch (TargetStat)
            {
                case ModifyStatType.AttackPower:
                    unit.AttackPower -= val;
                    break;
                case ModifyStatType.MaxHP:
                    unit.MaxHP -= val;
                    // 当前生命不随上限减少，仅超出新上限时截断
                    unit.CurrentHP = Mathf.Min(unit.CurrentHP, unit.MaxHP);
                    break;
                case ModifyStatType.Stamina:
                    unit.Stamina -= val;
                    break;
                case ModifyStatType.AttackDistance:
                    unit.AttackDistance -= val;
                    break;
                case ModifyStatType.ActionPoints:
                    unit.MaxActionPoints = System.Math.Max(1, unit.MaxActionPoints - val);
                    unit.ActionPoints = System.Math.Min(unit.ActionPoints, unit.MaxActionPoints);
                    break;
            }

            unit.UpdateUnit();
            GD.Print($"[ModifyStatAction] 还原 {TargetStat} {val:+0;-0} → {unit.UnitData?.UnitName}");
        }

        ctx.TargetUnit = originalTarget;
    }
}