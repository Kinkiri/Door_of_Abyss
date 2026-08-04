using Godot;

/// <summary>
/// 坐标偏移值源：基准坐标 + (dx, dy) 偏移。
/// dx/dy 支持固定值与动态值源覆盖（值源优先，遵循"固定值+值源"双字段惯例）。
/// </summary>
[GlobalClass]
public partial class OffsetCellValue : CellValueSource
{
    /// <summary>基准坐标（null = 无有效坐标）</summary>
    [Export] public CellValueSource Base { get; set; }

    [Export] public int Dx { get; set; }
    [Export] public ValueSource DxValueSource { get; set; }

    [Export] public int Dy { get; set; }
    [Export] public ValueSource DyValueSource { get; set; }

    public override Vector2I? GetCell(Context ctx)
    {
        if (Base == null) return null;
        var basePos = Base.GetCell(ctx);
        if (basePos == null) return null;

        int dx = DxValueSource?.GetValue(ctx) ?? Dx;
        int dy = DyValueSource?.GetValue(ctx) ?? Dy;
        return new Vector2I(basePos.Value.X + dx, basePos.Value.Y + dy);
    }
}
