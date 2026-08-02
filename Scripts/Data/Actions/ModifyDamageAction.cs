using Godot;

/// <summary>
/// 修改本次伤害事件的伤害量（正=加伤，负=减伤）。
/// 配合攻击前/受击前事件使用：攻击者挂加伤被动（TriggerEvent=OnBeforeAttack），
/// 受击者挂减伤被动（TriggerEvent=OnBeforeTakeDamage）。
/// 作用于各自 ctx.DamageModifier，由 DamageAction 结算时两侧累加（多个加伤/减伤被动可叠加）。
/// 可用 ValueSource 动态计算增量（如 `FormulaValue(Mul, PendingDamageValue, ConstantValue(-1))` 把伤害清零）。
/// </summary>
[GlobalClass]
public partial class ModifyDamageAction : GameAction
{
    [Export] public int Delta { get; set; }

    /// <summary>动态增量值源，设置后覆盖 Delta</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        ctx.DamageModifier += ValueSource?.GetValue(ctx) ?? Delta;
    }

    // 一次性修饰，无持久状态，不需要 Revert
}
