using Godot;

/// <summary>
/// 全地图形状：返回地图全部格子（忽略 center）。
/// 用于"随机全图一格"（RandomCellValue）等场景；区域解析走枚举 All 路径即可。
/// </summary>
[GlobalClass]
public partial class AllShape : CellShape
{
    public override Cell[] GetCells(Cell center, Context ctx) => TargetResolver.AllCells(ctx.Map);

    public override TargetShape GetCategory() => TargetShape.All;
}
