using Godot;

/// <summary>
/// OR 组合筛选器：任一子筛选器命中即保留（顺序无关）。
/// </summary>
[GlobalClass]
public partial class OrCardFilter : CardFilter
{
    /// <summary>子筛选器（顺序无关）</summary>
    [Export] public CardFilter[] Filters { get; set; }

    public override bool IsMatch(Card card)
    {
        if (Filters == null || Filters.Length == 0) return true;
        foreach (var f in Filters)
            if (f != null && f.IsMatch(card)) return true;
        return false;
    }
}
