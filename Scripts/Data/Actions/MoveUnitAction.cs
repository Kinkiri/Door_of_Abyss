using Godot;
using System.Collections.Generic;

/// <summary>
/// 强制位移动作：将目标单位（TargetUnits）拉拽、击退或传送到指定位置。不消耗 AP、不检查行动次数。
/// 与其他 GameAction（伤害/治疗/Buff）一致，作用于 TargetUnits：
/// - 卡牌路径 = 卡牌 TargetFilter 筛选出的目标；被动路径 = TargetFilter 解析或 Target=Self/EventOther。
/// - Push（击退）= 目标沿"锚点 → 目标"方向远离；Pull（拉拽）= 靠近锚点。
/// - 锚点 = DirectionAnchor（坐标值源，可推离门口/环境格等）→ SourceUnit（施法者/被动所有者）。
/// - 方向可用 DirectionValueSource 显式指定（值源优先），否则自动（锚点 → 目标，曼哈顿单轴）。
/// </summary>
[GlobalClass]
public partial class MoveUnitAction : GameAction
{
    /// <summary>移动模式</summary>
    public enum MoveMode { Teleport, Push, Pull }

    [Export] public MoveMode Mode { get; set; } = MoveMode.Teleport;

    /// <summary>Push/Pull 的距离格数（DistanceValueSource 配置后覆盖）</summary>
    [Export] public int Distance { get; set; } = 1;

    /// <summary>Push/Pull 动态距离值源，配置后覆盖 Distance（如"按自身攻击力击退"）</summary>
    [Export] public ValueSource DistanceValueSource { get; set; }

    /// <summary>
    /// 传送落点坐标（Teleport 模式）：非空且有有效坐标时直接传送到该坐标（覆盖 TargetCell/TargetUnit）。
    /// 坐标无效/格子不存在时静默跳过。
    /// </summary>
    [Export] public CellValueSource TeleportPosition { get; set; }

    /// <summary>
    /// Push/Pull 方向值源（值源优先）：配置后方向固定为值源（CellDirection 枚举值，
    /// 如 DirectionValue 动态朝向、ConstantValue 配固定 4 向）；非法值按 Up 处理。
    /// 未配置时用"锚点 → 目标"自动方向（曼哈顿单轴）。
    /// </summary>
    [Export] public ValueSource DirectionValueSource { get; set; }

    /// <summary>
    /// Push/Pull 锚点坐标（坐标值源）：配置后自动方向 = 锚点坐标 → 目标（可推离门口/环境格/任意坐标）；
    /// 未配置时锚点 = SourceUnit（施法者/被动所有者）。两者都无 → 不动作。
    /// </summary>
    [Export] public CellValueSource DirectionAnchor { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnits == null || ctx.TargetUnits.Length == 0) return;
        var map = ctx.Map ?? MapManager.Instance?.Map;
        if (map == null) return;

        foreach (var unit in ctx.TargetUnits)
        {
            if (unit == null || !unit.IsAlive || unit.IsDead) continue;

            Vector2I targetPos = ComputeTarget(unit, ctx, map);
            if (targetPos == unit.GridPos) continue;

            // 执行移动（不消耗 AP）
            UnitManager.Instance.TeleportUnit(unit, targetPos);
            GD.Print($"[MoveUnitAction] {Mode} {unit.UnitData?.UnitName} → {targetPos}");
        }
    }

    /// <summary>计算单个目标的位移落点（internal：供测试直接验证方向/距离/边界逻辑）</summary>
    internal Vector2I ComputeTarget(Unit unit, Context ctx, Dictionary<Vector2I, Cell> map)
    {
        if (Mode == MoveMode.Teleport)
        {
            // 传送到指定坐标（优先）→ TargetCell → TargetUnit 所在格
            if (TeleportPosition != null)
            {
                var pos = TeleportPosition.GetCell(ctx);
                if (pos == null) return unit.GridPos;
                if (!map.TryGetValue(pos.Value, out Cell posCell) || posCell == null) return unit.GridPos;
                return posCell.GridPos;
            }
            if (ctx.TargetCell != null) return ctx.TargetCell.GridPos;
            if (ctx.TargetUnit != null) return ctx.TargetUnit.GridPos;
            return unit.GridPos;
        }

        // ── Push/Pull ──────────────────────────────────────
        Vector2I dir;
        var dirValue = DirectionValueSource?.GetValue(ctx);
        if (dirValue.HasValue)
        {
            // 显式方向（值源优先）：CellDirection 枚举值
            int d = dirValue.Value;
            if (d < (int)CellDirection.Up || d > (int)CellDirection.Right) d = (int)CellDirection.Up;
            dir = TargetResolver.CellDirectionVector((CellDirection)d);
        }
        else
        {
            // 自动方向：锚点 → 目标（曼哈顿单轴）
            Vector2I anchorPos;
            if (DirectionAnchor != null)
            {
                var anchor = DirectionAnchor.GetCell(ctx);
                if (anchor == null) return unit.GridPos;
                anchorPos = anchor.Value;
            }
            else if (ctx.SourceUnit != null)
            {
                anchorPos = ctx.SourceUnit.GridPos;
            }
            else
            {
                return unit.GridPos;
            }

            Vector2I delta = unit.GridPos - anchorPos;
            dir = System.Math.Abs(delta.X) >= System.Math.Abs(delta.Y)
                ? new Vector2I(System.Math.Sign(delta.X), 0)
                : new Vector2I(0, System.Math.Sign(delta.Y));
        }

        // Push = 远离锚点（dir 已是"锚点 → 目标"）；Pull = 靠近（取反）
        if (Mode == MoveMode.Pull)
            dir = -dir;

        int dist = DistanceValueSource?.GetValue(ctx) ?? Distance;
        if (dist < 0) dist = 0;

        Vector2I target = unit.GridPos;
        for (int i = 0; i < dist; i++)
        {
            Vector2I next = target + dir;
            if (!map.TryGetValue(next, out Cell cell) || !cell.CanStand || cell.OccupyingUnit != null)
                break;
            target = next;
        }
        return target;
    }
}
