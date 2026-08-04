using Godot;

/// <summary>
/// 格子形状抽象基类：以中心格为中心生成形状内格子集合（多态 Resource，与 TargetFilter/Condition/ValueSource 同风格）。
/// 解析（ShapeTargetFilter.CustomShape）与预览（SelectionManager）共用同一套格子生成算法。
/// 语义约定：生成结果**含中心格**；仅返回存在于 ctx.Map 中的格（越界自动过滤）；center=null → 空数组。
/// </summary>
[GlobalClass]
public abstract partial class CellShape : Resource
{
    /// <summary>以 center 为中心生成形状内格子（含中心格，经 ctx.Map 过滤存在性）</summary>
    public abstract Cell[] GetCells(Cell center, Context ctx);

    /// <summary>
    /// 带尺寸覆盖的格子生成：sizeOverride ≥ 0 时主尺寸（Length/AreaRange）取它（攻击范围场景 = 单位射程联动），
    /// 否则回退形状自身参数（值源 → 静态字段）。不修改共享 Resource 实例，无状态污染。
    /// 默认实现转调无参版本（不覆盖尺寸的形状无需重写）。
    /// </summary>
    public virtual Cell[] GetCells(Cell center, Context ctx, int sizeOverride) => GetCells(center, ctx);

    /// <summary>类别枚举（UI 预览/校验/文本用；穿透组合用 GetShape() 判断形状节点）</summary>
    public abstract TargetShape GetCategory();

    /// <summary>扩散半径（预览/文本用；形状参数为值源时动态取值）</summary>
    public virtual int GetAreaRange() => 1;

    /// <summary>形状显示描述："十字 2" 等（size = 联动尺寸，如单位射程）</summary>
    public string Describe(int size) => $"{CategoryName(GetCategory())} {size}";

    /// <summary>攻击范围显示：null 形状（默认菱形）只显示数字，否则形状描述（如"十字 2"）</summary>
    public static string DescribeRange(CellShape shape, int size)
        => shape == null ? $"{size}" : shape.Describe(size);

    private static string CategoryName(TargetShape s) => s switch
    {
        TargetShape.AreaDiamond => "菱形",
        TargetShape.AreaSquare => "方形",
        TargetShape.Cross => "十字",
        TargetShape.X => "叉",
        TargetShape.Ray => "射线",
        TargetShape.Triangle => "三角",
        TargetShape.Row => "行",
        TargetShape.Column => "列",
        TargetShape.Ring => "环",
        TargetShape.All => "全图",
        _ => "单体",
    };
}
