using Godot;

/// <summary>
/// 当前回合数值源。
/// </summary>
[GlobalClass]
public partial class RoundCountValue : ValueSource
{
    public override int GetValue(Context ctx)
    {
        // BattleManager.RoundCount 或者类似的字段？
        // 目前 BattleManager 有一个 RoundCount 属性
        var bm = BattleManager.Instance;
        return bm?.RoundCount ?? 0;
    }
}
