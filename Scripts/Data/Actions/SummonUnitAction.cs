using Godot;

/// <summary>
/// 在目标格子召唤单位。单位数据来自 SourceCard.CardData（需为 UnitCardData）。
/// 召唤位置必须在己方门周围的部署范围内（LevelData.DeployRange 控制，默认 2 格曼哈顿距离）。
/// </summary>
[GlobalClass]
public partial class SummonUnitAction : GameAction
{
    protected override void Apply(Context ctx)
    {
        if (ctx.TargetCell == null) return;
        if (ctx.SourceCard?.CardData is not UnitCardData unitCard) return;

        // 检查是否在门附近的部署范围内
        if (ctx.SourceTeam == Team.Player)
        {
            var door = UnitManager.Instance?.PlayerDoor;
            int range = (door?.UnitData as DoorData)?.DeployRange ?? 2;
            if (door != null && !IsWithinRange(ctx.TargetCell.GridPos, door.GridPos, range))
            {
                int dist = ManhattanDist(ctx.TargetCell.GridPos, door.GridPos);
                GD.Print($"[SummonUnitAction] 超出部署范围: 门在 {door.GridPos}，目标 {ctx.TargetCell.GridPos}，距离 {dist} > {range}");
                return;
            }
        }

        var spawned = UnitManager.Instance.SpawnUnit(
            unitCard.UnitData, ctx.TargetCell.GridPos, ctx.SourceTeam);
        if (spawned != null)
            GD.Print($"[SummonUnitAction] 召唤 {unitCard.UnitData.UnitName} 于 {ctx.TargetCell.GridPos}");
    }

    private static bool IsWithinRange(Vector2I pos, Vector2I doorPos, int range)
    {
        return ManhattanDist(pos, doorPos) <= range;
    }

    private static int ManhattanDist(Vector2I a, Vector2I b)
    {
        return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
    }
}
