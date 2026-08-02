using Godot;

/// <summary>
/// 卡牌筛选器抽象基类。与 TargetFilter（单位/格子目标解析）同风格的多态组合体系，
/// 用于"按特征检索牌库"（如抽一张法术牌）。具体筛选逻辑由子类实现（静态属性 / 逻辑组合），
/// 组合类递归穿透子节点。
///
/// 语义约定：
/// - 引用为 null 表示"不限制"（筛选抽牌退化为普通抽牌），由调用方判断；
/// - 数组 → AND 组合（CombineAnd）：null/空 → null；单元素 → 原样；多元素 → AndCardFilter；
/// - 谓词基于 Card 运行时实例（IsMatch），属性读取经 CardData 模板。
/// </summary>
[GlobalClass]
public abstract partial class CardFilter : Resource
{
    /// <summary>单卡匹配谓词；默认全部匹配</summary>
    public virtual bool IsMatch(Card card) => true;

    /// <summary>数组 → AND 组合（默认 And 逻辑）</summary>
    public static CardFilter CombineAnd(CardFilter[] filters)
    {
        if (filters == null || filters.Length == 0) return null;
        var valid = new System.Collections.Generic.List<CardFilter>(filters.Length);
        foreach (var f in filters)
            if (f != null)
                valid.Add(f);
        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];
        return new AndCardFilter { Filters = valid.ToArray() };
    }
}
