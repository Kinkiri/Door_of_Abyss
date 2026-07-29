using Godot;

/// <summary>
/// 随机值源，返回 [Min, Max] 区间内的随机整数。
/// </summary>
[GlobalClass]
public partial class RandomValue : ValueSource
{
    [Export] public int Min { get; set; } = 0;
    [Export] public int Max { get; set; } = 100;

    private static readonly System.Random _rng = new();

    public override int GetValue(Context ctx)
    {
        return _rng.Next(Min, Max + 1);
    }
}
