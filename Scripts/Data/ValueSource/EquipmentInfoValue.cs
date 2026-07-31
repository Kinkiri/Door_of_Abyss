using Godot;

/// <summary>
/// 装备信息值源，从 Context 中的来源或目标单位上读取装备信息。
/// 无装备时：HasEquipment 返回 0，其余返回 DefaultValue。
/// </summary>
[GlobalClass]
public partial class EquipmentInfoValue : ValueSource
{
    [Export] public ValueTarget Unit { get; set; } = ValueTarget.Target;

    [Export] public EquipmentInfoType Info { get; set; } = EquipmentInfoType.HasEquipment;

    /// <summary>无装备（且非 HasEquipment）时的默认返回值</summary>
    [Export] public int DefaultValue { get; set; } = 0;

    public override int GetValue(Context ctx)
    {
        var unit = Unit == ValueTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null) return DefaultValue;

        var equip = EquipmentManager.Instance?.GetEquipment(unit);
        if (equip == null)
            return Info == EquipmentInfoType.HasEquipment ? 0 : DefaultValue;

        return Info switch
        {
            EquipmentInfoType.HasEquipment => 1,
            EquipmentInfoType.AttackBonus => equip.Data?.AttackBonus ?? 0,
            EquipmentInfoType.MaxHealthBonus => equip.Data?.MaxHealthBonus ?? 0,
            EquipmentInfoType.AttackDistanceBonus => equip.Data?.AttackDistanceBonus ?? 0,
            EquipmentInfoType.StaminaBonus => equip.Data?.StaminaBonus ?? 0,
            EquipmentInfoType.ActionPointBonus => equip.Data?.ActionPointBonus ?? 0,
            _ => DefaultValue,
        };
    }
}
