using Godot;

/// <summary>
/// AND 组合筛选器：全部子筛选器命中才保留（顺序无关）。
/// </summary>
[GlobalClass]
public partial class AndCardFilter : CardFilter
{
    /// <summary>子筛选器（顺序无关）</summary>
    [Export] public CardFilter[] Filters { get; set; }

    public override bool IsMatch(Card card)
    {
        if (Filters == null || Filters.Length == 0) return true;
        foreach (var f in Filters)
            if (f != null && !f.IsMatch(card)) return false;
        return true;
    }
}
