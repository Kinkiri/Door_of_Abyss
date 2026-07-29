using Godot;

/// <summary>
/// 抽牌。
/// </summary>
[GlobalClass]
public partial class DrawCardAction : GameAction
{
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        int count = ValueSource?.GetValue(ctx) ?? Value;
        var drawn = CardManager.Instance?.DrawCards(count);
        if (drawn != null && drawn.Count > 0)
            GD.Print($"[DrawCardAction] 抽 {drawn.Count} 张牌");
    }
}
