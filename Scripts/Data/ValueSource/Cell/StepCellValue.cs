using Godot;

/// <summary>
/// 方向步进值源：基准坐标沿指定方向走 N 格。
/// 方向/距离均支持固定值与动态值源覆盖（值源优先）；距离 ≤ 0 返回基准坐标。
/// 不做地图校验（可链式组合），越界坐标由最终消费方处理。
/// </summary>
[GlobalClass]
public partial class StepCellValue : CellValueSource
{
    /// <summary>基准坐标（null = 无有效坐标）</summary>
    [Export] public CellValueSource Base { get; set; }

    [Export] public CellDirection Direction { get; set; } = CellDirection.Up;

    /// <summary>动态方向值源（如 DirectionValue 计算两点朝向），配置后覆盖 Direction；非法值按 Up 处理</summary>
    [Export] public ValueSource DirectionValueSource { get; set; }

    [Export] public int Distance { get; set; } = 1;

    /// <summary>动态距离值源，配置后覆盖 Distance</summary>
    [Export] public ValueSource DistanceValueSource { get; set; }

    private static readonly Vector2I[] DirVectors =
    {
        new Vector2I(0, -1),  // Up
        new Vector2I(0, 1),   // Down
        new Vector2I(-1, 0),  // Left
        new Vector2I(1, 0),   // Right
    };

    public override Vector2I? GetCell(Context ctx)
    {
        if (Base == null) return null;
        var basePos = Base.GetCell(ctx);
        if (basePos == null) return null;

        int dir = DirectionValueSource?.GetValue(ctx) ?? (int)Direction;
        if (dir < (int)CellDirection.Up || dir > (int)CellDirection.Right) dir = (int)CellDirection.Up;

        int dist = DistanceValueSource?.GetValue(ctx) ?? Distance;
        if (dist <= 0) return basePos;

        var v = DirVectors[dir];
        return basePos.Value + v * dist;
    }
}
