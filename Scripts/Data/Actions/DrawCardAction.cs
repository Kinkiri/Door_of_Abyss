using Godot;

/// <summary>
/// 抽牌。设置 Filters 时从牌库随机抽取匹配的牌（仅牌库，无匹配不抽，不足全要）；否则抽牌库顶。
/// </summary>
[GlobalClass]
public partial class DrawCardAction : GameAction
{
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    /// <summary>筛选条件（数组默认 And），非空时只从牌库随机抽取匹配的牌</summary>
    [Export] public CardFilter[] Filters { get; set; }

    protected override void Apply(Context ctx)
    {
        int count = ValueSource?.GetValue(ctx) ?? Value;
        var filter = CardFilter.CombineAnd(Filters);
        var drawn = filter != null
            ? CardManager.Instance?.DrawCards(count, filter)
            : CardManager.Instance?.DrawCards(count);
        if (drawn != null && drawn.Count > 0)
            GD.Print($"[DrawCardAction] 抽 {drawn.Count} 张牌");
    }
}
