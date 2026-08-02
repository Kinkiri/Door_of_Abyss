using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 随机筛选器：从上游候选（已筛选过的目标组）随机取 Count 个，均匀无偏且不重复。
/// 数量不足 Count 时全部保留（与极值"数量不足全要"语义一致）。后处理节点（GetShape=None，不生成候选）。
/// 例："随机 1 个敌方" → [Shape(全体), Team(敌方), Random(1)]；"随机 2 格放环境" → [Shape(全体), Random(2)]
/// </summary>
[GlobalClass]
public partial class RandomTargetFilter : TargetFilter
{
    /// <summary>随机抽取数量；候选不足时全部保留；0 或负数 = 空结果</summary>
    [Export] public int Count { get; set; } = 1;

    /// <summary>动态数量值源（如 UnitCountValue），配置后覆盖 Count</summary>
    [Export] public ValueSource ValueSource { get; set; }

    /// <summary>Fisher-Yates 部分洗牌取前 n 个：均匀无偏、不重复、不改上游数组</summary>
    private static T[] TakeRandom<T>(T[] list, int n)
    {
        var pool = new List<T>(list);
        for (int i = 0; i < n; i++)
        {
            int j = i + GD.RandRange(0, pool.Count - 1 - i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, n).ToArray();
    }

    public override Unit[] ApplyUnits(Unit[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllAliveUnits(ctx.ActiveUnits);
        int n = ValueSource?.GetValue(ctx) ?? Count;
        if (n <= 0) return Array.Empty<Unit>();
        if (list.Length <= n) return list;
        return TakeRandom(list, n);
    }

    public override Cell[] ApplyCells(Cell[] candidates, Context ctx)
    {
        var list = candidates ?? TargetResolver.AllCells(ctx.Map);
        int n = ValueSource?.GetValue(ctx) ?? Count;
        if (n <= 0) return Array.Empty<Cell>();
        if (list.Length <= n) return list;
        return TakeRandom(list, n);
    }

    public override TargetShape GetShape() => TargetShape.None;
}
