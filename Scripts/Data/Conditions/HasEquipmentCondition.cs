using Godot;

/// <summary>
/// 装备条件：检查来源或目标是否拥有指定 ID 的装备。
/// EquipmentID 留空时仅判断"是否有任何装备"。
/// </summary>
[GlobalClass]
public partial class HasEquipmentCondition : Condition
{
    [Export(PropertyHint.Enum, "来源,目标")] public ConditionTarget CheckTarget { get; set; } = ConditionTarget.Target;

    /// <summary>要检查的 EquipmentID，留空=任意装备</summary>
    [Export] public string EquipmentID { get; set; } = "";

    /// <summary>true=必须拥有，false=必须没有</summary>
    [Export] public bool Has { get; set; } = true;

    public override bool IsMet(Context ctx)
    {
        var unit = CheckTarget == ConditionTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null) return false;

        var equip = EquipmentManager.Instance?.GetEquipment(unit);
        bool hasEquip = equip != null && (string.IsNullOrEmpty(EquipmentID)
            || equip.Data?.EquipmentID == EquipmentID);
        return hasEquip == Has;
    }
}
