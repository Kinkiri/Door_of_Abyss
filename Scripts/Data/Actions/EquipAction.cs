using Godot;

/// <summary>
/// 给目标单位装备装备。装备数据来自 SourceCard.CardData（需为 EquipmentCardData）。
/// 属性加成可逆：卸载由 EquipmentManager.RemoveEquipment 负责还原。
/// </summary>
[GlobalClass]
public partial class EquipAction : GameAction
{
    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null) return;
        if (ctx.SourceCard?.CardData is not EquipmentCardData equipCard) return;
        if (equipCard.EquipmentData == null)
        {
            GD.PrintErr($"[EquipAction] 装备卡 {equipCard.CardID} 未配置 EquipmentData");
            return;
        }

        EquipmentManager.Instance?.Equip(ctx.TargetUnit, equipCard.EquipmentData, ctx.SourceUnit);
    }
}
