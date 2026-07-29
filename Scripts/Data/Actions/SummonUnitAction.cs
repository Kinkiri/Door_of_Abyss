using Godot;

/// <summary>
/// 在目标格子召唤单位。单位数据来自 SourceCard.CardData（需为 UnitCardData）。
/// </summary>
[GlobalClass]
public partial class SummonUnitAction : GameAction
{
    protected override void Apply(Context ctx)
    {
        if (ctx.TargetCell == null) return;
        if (ctx.SourceCard?.CardData is not UnitCardData unitCard) return;

        var spawned = UnitManager.Instance.SpawnUnit(
            unitCard.UnitData, ctx.TargetCell.GridPos, ctx.SourceTeam);
        if (spawned != null)
            GD.Print($"[SummonUnitAction] 召唤 {unitCard.UnitData.UnitName} 于 {ctx.TargetCell.GridPos}");
    }
}
