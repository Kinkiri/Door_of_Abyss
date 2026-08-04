using Godot;

/// <summary>
/// 攻击方向值源：读取 ctx.AttackDirection（攻击事件 = 攻击者 → 受击者的曼哈顿 4 向），返回 CellDirection 枚举值。
/// 非攻击事件（无攻击方向）时返回 DefaultValue。配合 CompareCondition 判断"背刺/侧翼"等。
/// </summary>
[GlobalClass]
public partial class AttackDirectionValue : ValueSource
{
    /// <summary>无攻击方向时的默认返回值（默认 0 = CellDirection.Up，按需配置）</summary>
    [Export] public int DefaultValue { get; set; } = 0;

    public override int GetValue(Context ctx)
        => ctx.AttackDirection.HasValue ? (int)ctx.AttackDirection.Value : DefaultValue;
}
