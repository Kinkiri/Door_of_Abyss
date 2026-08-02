using Godot;

/// <summary>
/// 修改格子运行时属性（目标为格子）。
/// MoveCost：数值加减，可逆（Revert 对称减回，供 Environment/Buff 移除时还原）。
/// CanStand/CanPass：布尔覆盖，**不可逆**（无法得知施加前值，对齐 SetStatAction 语义，
/// 不能放入 OnApplyActions——格子布尔覆盖请用 EnvironmentData 的 CanStandOverride/CanPassOverride）。
/// </summary>
[GlobalClass]
public partial class ModifyCellStatAction : GameAction
{
    [Export] public CellStatType TargetStat { get; set; } = CellStatType.MoveCost;

    [Export] public int Value { get; set; } = 1;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    private Cell[] ResolveCells(Context ctx)
    {
        if (ctx.TargetCells != null && ctx.TargetCells.Length > 0)
            return ctx.TargetCells;
        if (ctx.TargetCell != null)
            return new[] { ctx.TargetCell };
        return null;
    }

    protected override void Apply(Context ctx)
    {
        var cells = ResolveCells(ctx);
        if (cells == null) return;

        foreach (var cell in cells)
        {
            if (cell == null) continue;
            int val = ValueSource?.GetValue(ctx) ?? Value;

            switch (TargetStat)
            {
                case CellStatType.MoveCost:
                    cell.MoveCost = System.Math.Max(0, cell.MoveCost + val);
                    break;
                case CellStatType.CanStand:
                    cell.CanStand = val != 0;
                    break;
                case CellStatType.CanPass:
                    cell.CanPass = val != 0;
                    break;
            }

            GD.Print($"[ModifyCellStatAction] {TargetStat} {val:+0;-0} → {cell.GridPos}");
        }
    }

    public override void Revert(Context ctx)
    {
        var cells = ResolveCells(ctx);
        if (cells == null) return;

        foreach (var cell in cells)
        {
            if (cell == null) continue;
            int val = ValueSource?.GetValue(ctx) ?? Value;

            switch (TargetStat)
            {
                case CellStatType.MoveCost:
                    cell.MoveCost = System.Math.Max(0, cell.MoveCost - val);
                    GD.Print($"[ModifyCellStatAction] 还原 MoveCost {-val} → {cell.GridPos}");
                    break;
                case CellStatType.CanStand:
                case CellStatType.CanPass:
                    GD.Print($"[ModifyCellStatAction] {TargetStat} 布尔覆盖不可逆，跳过还原");
                    break;
            }
        }
    }
}
