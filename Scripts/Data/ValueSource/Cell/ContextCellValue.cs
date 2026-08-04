using Godot;

/// <summary>Context 格子取值类型</summary>
public enum ContextCellType
{
    /// <summary>事件目标格（死亡格/进入格/环境格/卡牌点击格）</summary>
    Target,

    /// <summary>事件来源格（移动前旧格/离开格）</summary>
    Source,
}

/// <summary>
/// Context 格子坐标值源：读取 ctx.TargetCell / ctx.SourceCell 的坐标。
/// 环境被动场景 ctx.TargetCell=环境所在格；死亡事件=死亡格；卡牌路径=点击格。
/// </summary>
[GlobalClass]
public partial class ContextCellValue : CellValueSource
{
    [Export] public ContextCellType Cell { get; set; } = ContextCellType.Target;

    public override Vector2I? GetCell(Context ctx)
    {
        var cell = Cell == ContextCellType.Target ? ctx.TargetCell : ctx.SourceCell;
        return cell?.GridPos;
    }
}
