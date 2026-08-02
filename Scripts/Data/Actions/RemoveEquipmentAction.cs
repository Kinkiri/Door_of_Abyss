using Godot;

/// <summary>
/// 移除目标单位上指定 ID 的装备（驱散）。
/// 与 RemoveBuffAction 对称：按 EquipmentID 匹配移除。
/// 属性加成还原（可逆）+ 取消被动订阅，由 EquipmentManager.RemoveEquipment 完成。
/// 单位同一时间只能装备一件；ID 不匹配或未装备时不做任何事。
/// </summary>
[GlobalClass]
public partial class RemoveEquipmentAction : GameAction
{
    /// <summary>要移除的装备 ID</summary>
    [Export] public string EquipmentID { get; set; } = "";

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null || string.IsNullOrEmpty(EquipmentID)) return;

        var equip = EquipmentManager.Instance?.GetEquipment(ctx.TargetUnit);
        if (equip == null || equip.Data?.EquipmentID != EquipmentID)
        {
            GD.Print($"[RemoveEquipmentAction] 未找到装备: {EquipmentID}（当前: {equip?.Data?.EquipmentID ?? "无"}）");
            return;
        }

        GD.Print($"[RemoveEquipmentAction] 移除 {EquipmentID} 于 {ctx.TargetUnit.UnitData?.UnitName}");
        EquipmentManager.Instance.RemoveEquipment(ctx.TargetUnit, equip);
    }
}
