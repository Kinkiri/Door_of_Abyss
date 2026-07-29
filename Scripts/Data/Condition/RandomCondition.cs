using Godot;

/// <summary>
/// 概率条件：指定概率通过判定。
/// </summary>
[GlobalClass]
public partial class RandomCondition : Condition
{
    /// <summary>通过概率 0.0~1.0</summary>
    [Export] public float Probability { get; set; } = 0.5f;

    private static readonly System.Random _rng = new();

    public override bool IsMet(Context ctx)
    {
        return _rng.NextDouble() < Probability;
    }
}
