using Godot;

/// <summary>
/// 目标筛选器抽象基类。具体筛选逻辑由子类实现（静态属性 / 动态条件 / 逻辑组合），
/// 组合类递归穿透子节点。与 Condition / ValueSource 体系同风格的多态组合模式。
///
/// 语义约定：
/// - 引用为 null 表示"无目标"（如无目标法术），由调用方判断；
/// - ApplyUnits/ApplyCells 的 candidates 为 null 表示"无上游候选"，从全量
///   （全部存活单位 / 地图全部格子）开始；
/// - 形状节点（ShapeTargetFilter）忽略传入候选，自行生成候选集；
/// - 过滤/组合节点对传入候选过滤（任一命中的谓词语义）。
/// </summary>
[GlobalClass]
public abstract partial class TargetFilter : Resource
{
    /// <summary>解析单位目标。candidates 为 null 时从全量活跃单位开始。</summary>
    public abstract Unit[] ApplyUnits(Unit[] candidates, Context ctx);

    /// <summary>解析格子目标。candidates 为 null 时从地图全部格子开始。</summary>
    public abstract Cell[] ApplyCells(Cell[] candidates, Context ctx);

    /// <summary>有效形状（穿透组合递归），供 UI 预览/校验使用；无形状返回 None</summary>
    public abstract TargetShape GetShape();

    /// <summary>有效扩散半径（穿透组合递归取形状节点的 AreaRange）；无形状时返回 1</summary>
    public virtual int GetAreaRange() => 1;

    /// <summary>
    /// 形状节点实例（穿透组合递归取第一个形状节点；无形状节点返回 null）。
    /// 供 UI 预览统一调用 CellShape.GetCells 生成预览格（与解析共用同一算法）。
    /// </summary>
    public virtual CellShape GetCellShape() => null;

    /// <summary>
    /// 结果集类型（单位/格子）：仅形状节点真正持有 Kind，
    /// 过滤/组合节点穿透取形状子节点的值；无形状时默认 Unit。
    /// </summary>
    public virtual TargetKind GetKind() => TargetKind.Unit;

    /// <summary>单单位匹配谓词（过滤与 UI 校验共用）；默认全部匹配</summary>
    public virtual bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx) => true;

    /// <summary>单格子匹配谓词；默认全部匹配</summary>
    public virtual bool IsCellMatch(Cell cell, Context ctx) => true;

    /// <summary>穿透组合取相对阵营（UI 高亮图标用）；默认 All</summary>
    public virtual TeamFilter GetTeamFilter() => TeamFilter.All;

    /// <summary>
    /// 数组 → AND 组合（默认 And 逻辑）：
    /// null/空 → null（无目标）；过滤 null 元素后单元素 → 原样；多元素 → 包 AndTargetFilter。
    /// </summary>
    public static TargetFilter CombineAnd(TargetFilter[] filters)
    {
        if (filters == null || filters.Length == 0) return null;
        var valid = new System.Collections.Generic.List<TargetFilter>(filters.Length);
        foreach (var f in filters)
            if (f != null)
                valid.Add(f);
        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];
        return new AndTargetFilter { Filters = valid.ToArray() };
    }
}
