using Godot;

/// <summary>费用取值方式</summary>
public enum CostValueType { Current, Max }

/// <summary>
/// 当前或最大费用值源。
/// </summary>
[GlobalClass]
public partial class BattleCostValue : ValueSource
{
    [Export] public CostValueType Type { get; set; } = CostValueType.Current;

    public override int GetValue(Context ctx)
    {
        var bm = BattleManager.Instance;
        if (bm == null) return 0;
        return Type switch
        {
            CostValueType.Current => bm.PlayerCost,
            CostValueType.Max => BattleManager.MaxCost,
            _ => 0,
        };
    }
}
