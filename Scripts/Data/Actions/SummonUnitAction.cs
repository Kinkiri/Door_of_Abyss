using Godot;
using System.Linq;

/// <summary>
/// 在目标格子召唤单位。
/// 通用召唤：配置 UnitData（Resource 引用）或 UnitID（字符串查 UnitLibrary，避免循环引用）
/// 即可召唤任意单位（无部署范围限制，可用于法术/被动/亡语重生）。
/// 单位卡路径：两者都未配置时回退到 SourceCard（须为 UnitCardData）自身的单位，
/// 且仅此路径保留"必须在己方门部署范围内"的检查（玩家出单位卡的限制）。
/// </summary>
[GlobalClass]
public partial class SummonUnitAction : GameAction
{
    /// <summary>通用召唤：直接指定要召唤的单位模板（null = 走 UnitID 或单位卡路径）</summary>
    [Export] public UnitData UnitData { get; set; }

    /// <summary>通用召唤：按 UnitID 从 UnitLibrary 查模板（亡语重生等循环引用场景用字符串规避）</summary>
    [Export] public string UnitID { get; set; } = "";

    /// <summary>召唤成功后自动给新单位施加的 Buff（null = 不施加）</summary>
    [Export] public BuffData SpawnBuff { get; set; }

    /// <summary>SpawnBuff 的初始层数</summary>
    [Export] public int SpawnBuffStacks { get; set; } = 1;

    protected override void Apply(Context ctx)
    {
        // 目标格子：优先遍历 TargetCells（区域召唤），单格回退 TargetCell
        var cells = (ctx.TargetCells != null && ctx.TargetCells.Length > 0)
            ? ctx.TargetCells
            : (ctx.TargetCell != null ? new[] { ctx.TargetCell } : null);
        if (cells == null) return;

        // 优先用动作配置的单位（UnitData 引用 → UnitID 查库），否则回退到单位卡自身的 UnitData
        var unitData = UnitData
            ?? (string.IsNullOrEmpty(UnitID) ? null : UnitLibrary.GetUnitByID(UnitID))
            ?? (ctx.SourceCard?.CardData as UnitCardData)?.UnitData;
        if (unitData == null) return;

        bool isUnitCardPath = UnitData == null && string.IsNullOrEmpty(UnitID);

        foreach (var cell in cells)
        {
            if (cell == null) continue;

            // 仅"单位卡路径"（未显式配 UnitData/UnitID）保留部署范围检查；通用召唤无限制
            if (isUnitCardPath && ctx.SourceTeam == Team.Player)
            {
                bool inRange = false;
                foreach (var door in UnitManager.GetDoors(Team.Player))
                {
                    int range = (door.UnitData as DoorData)?.DeployRange ?? 2;
                    if (IsWithinRange(cell.GridPos, door.GridPos, range))
                    { inRange = true; break; }
                }
                if (!inRange)
                {
                    string doorInfo = string.Join(", ", UnitManager.GetDoors(Team.Player).Select(d => $"{d.UnitData?.UnitName}@{d.GridPos}"));
                    GD.Print($"[SummonUnitAction] 超出所有门部署范围: 目标 {cell.GridPos}，门: {doorInfo}");
                    continue;
                }
            }

            var spawned = UnitManager.Instance.SpawnUnit(
                unitData, cell.GridPos, ctx.SourceTeam);
            if (spawned != null)
            {
                if (SpawnBuff != null)
                {
                    BuffManager.Instance?.ApplyBuff(spawned, SpawnBuff, ctx.SourceUnit, SpawnBuffStacks);
                    GD.Print($"[SummonUnitAction] 召唤 {unitData.UnitName} 于 {cell.GridPos}" +
                             $" +{SpawnBuffStacks}层{SpawnBuff.BuffName}");
                }
                else
                {
                    GD.Print($"[SummonUnitAction] 召唤 {unitData.UnitName} 于 {cell.GridPos}");
                }
            }
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
