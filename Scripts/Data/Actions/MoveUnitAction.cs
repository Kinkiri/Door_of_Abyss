using Godot;

/// <summary>
/// 强制位移动作：将来源单位拉拽、击退或传送到指定位置。
/// 不消耗 AP、不检查行动次数。
/// 位置由 TargetCell 或 TargetUnit 的格子决定。
/// </summary>
[GlobalClass]
public partial class MoveUnitAction : GameAction
{
    /// <summary>移动模式</summary>
    public enum MoveMode { Teleport, Push, Pull }

    [Export] public MoveMode Mode { get; set; } = MoveMode.Teleport;

    /// <summary>Push/Pull 的距离格数</summary>
    [Export] public int Distance { get; set; } = 1;

    protected override void Apply(Context ctx)
    {
        if (ctx.SourceUnit == null) return;
        var map = MapManager.Instance?.Map;
        if (map == null) return;

        Vector2I targetPos = ctx.SourceUnit.GridPos;
        Vector2I sourcePos = ctx.SourceUnit.GridPos;

        if (Mode == MoveMode.Teleport)
        {
            // 传送到 TargetCell 或 TargetUnit 所在位置
            if (ctx.TargetCell != null)
                targetPos = ctx.TargetCell.GridPos;
            else if (ctx.TargetUnit != null)
                targetPos = ctx.TargetUnit.GridPos;
            else
                return;
        }
        else if (Mode == MoveMode.Push || Mode == MoveMode.Pull)
        {
            // 必须有目标才能确定方向
            Unit anchor = ctx.TargetUnit;
            if (anchor == null) return;

            Vector2I anchorPos = anchor.GridPos;
            Vector2I dir = anchorPos - sourcePos;

            // 取曼哈顿方向（单轴）
            if (System.Math.Abs(dir.X) >= System.Math.Abs(dir.Y))
                dir = new Vector2I(System.Math.Sign(dir.X), 0);
            else
                dir = new Vector2I(0, System.Math.Sign(dir.Y));

            if (Mode == MoveMode.Pull)
                dir = -dir;

            targetPos = sourcePos;
            for (int i = 0; i < Distance; i++)
            {
                Vector2I next = targetPos + dir;
                if (!map.TryGetValue(next, out Cell cell) || !cell.CanStand || cell.OccupyingUnit != null)
                    break;
                targetPos = next;
            }
        }

        if (targetPos == ctx.SourceUnit.GridPos) return;

        // 执行移动（不消耗 AP）
        UnitManager.Instance.TeleportUnit(ctx.SourceUnit, targetPos);
        GD.Print($"[MoveUnitAction] {Mode} {ctx.SourceUnit.UnitData?.UnitName} → {targetPos}");
    }
}
