using Godot;
using System.Linq;

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

        // 检查目标是否在任意门的部署范围内
        if (ctx.SourceTeam == Team.Player)
        {
            bool inRange = false;
            foreach (var door in UnitManager.GetDoors(Team.Player))
            {
                int range = (door.UnitData as DoorData)?.DeployRange ?? 2;
                if (IsWithinRange(ctx.TargetCell.GridPos, door.GridPos, range))
                { inRange = true; break; }
            }
            if (!inRange)
            {
                string doorInfo = string.Join(", ", UnitManager.GetDoors(Team.Player).Select(d => $"{d.UnitData?.UnitName}@{d.GridPos}"));
                GD.Print($"[SummonUnitAction] 超出所有门部署范围: 目标 {ctx.TargetCell.GridPos}，门: {doorInfo}");
                return;
            }
        }

        var spawned = UnitManager.Instance.SpawnUnit(
            unitCard.UnitData, ctx.TargetCell.GridPos, ctx.SourceTeam);
        if (spawned != null)
        {
            ctx.SpawnedUnit = spawned;
            GD.Print($"[SummonUnitAction] 召唤 {unitCard.UnitData.UnitName} 于 {ctx.TargetCell.GridPos}");
        }
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
