using Godot;

/// <summary>
/// 增减玩家费用（法力值）。
/// 正值 = 增加费用，负值 = 消耗费用。
/// 自动钳制在 [0, MaxCost] 范围内。
/// </summary>
[GlobalClass]
public partial class ModifyCostAction : GameAction
{
    [Export] public int Value;

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        if (BattleManager.Instance == null) return;
        int delta = ValueSource?.GetValue(ctx) ?? Value;
        BattleManager.Instance.ModifyPlayerCost(delta);
        GD.Print($"[ModifyCostAction] 费用 {delta:+0;-0} → 当前 {BattleManager.Instance.PlayerCost}");
    }
}
