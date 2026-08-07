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

    /// <summary>仅修改当前行动点（上限不动，clamp 到 [0, MaxActionPoints]）；false = 默认上限+当前同步增减</summary>
    [Export] public bool CurrentAPOnly { get; set; }

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
        // 注意：Revert 由 BuffManager/EquipmentManager 等直接调用（不经过 Execute 的
        // ResolveTargets 包装），调用方只传 TargetUnit；因此这里必须兼容
        // "TargetUnits 为空但 TargetUnit 非空"的单目标 fallback，不能像 Damage/Heal 那样
        // 直接 if (TargetUnits == null) return。
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
                    // 当前生命随上限增减走统一 HP 入口（clamp/浮动数字/致死；val 负扣到 0 正常死亡）
                    UnitManager.Instance?.ApplyRawHPChange(unit, val, lethal: true);
                    break;
                case ModifyStatType.Stamina:
                    unit.Stamina += val;
                    break;
                case ModifyStatType.AttackDistance:
                    unit.AttackDistance += val;
                    break;
                case ModifyStatType.ActionPoints:
                    if (CurrentAPOnly)
                        // 仅当前 AP：允许超过上限（"本回合多动一次"类透支效果），只 clamp 下限 0
                        unit.ActionPoints = System.Math.Max(0, unit.ActionPoints + val);
                    else
                    {
                        unit.MaxActionPoints += val;
                        unit.ActionPoints += val;
                    }
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
        // Revert 不经 ResolveTargets：BuffManager.RemoveBuff / RemoveAllBuffs、
        // EquipmentManager 移除、ModifyBuffAction 减层均直接构造 { TargetUnit = x } 调用，
        // 必须回退到单目标，否则属性无法还原。
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
                    // 当前生命不随上限减少，仅超出新上限时截断；截断量走统一 HP 入口补伤害反馈（还原不致死）
                    int hpDelta = Mathf.Min(unit.CurrentHP, unit.MaxHP) - unit.CurrentHP;
                    if (hpDelta < 0) UnitManager.Instance?.ApplyRawHPChange(unit, hpDelta, lethal: false);
                    break;
                case ModifyStatType.Stamina:
                    unit.Stamina -= val;
                    break;
                case ModifyStatType.AttackDistance:
                    unit.AttackDistance -= val;
                    break;
                case ModifyStatType.ActionPoints:
                    if (CurrentAPOnly)
                        unit.ActionPoints = System.Math.Max(0, unit.ActionPoints - val);
                    else
                    {
                        unit.MaxActionPoints = System.Math.Max(1, unit.MaxActionPoints - val);
                        unit.ActionPoints = System.Math.Min(unit.ActionPoints, unit.MaxActionPoints);
                    }
                    break;
            }

            unit.UpdateUnit();
            GD.Print($"[ModifyStatAction] 还原 {TargetStat} {val:+0;-0} → {unit.UnitData?.UnitName}");
        }

        ctx.TargetUnit = originalTarget;
    }
}