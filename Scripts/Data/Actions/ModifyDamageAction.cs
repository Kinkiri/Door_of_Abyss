using Godot;

/// <summary>
/// 修改本次伤害事件的伤害量（正=加伤，负=减伤）。
/// 配合 OnBeforeDamage 事件使用：作用于 ctx.DamageModifier，
/// 由 DamageAction 结算时应用（多个加伤/减伤被动可叠加）。
/// </summary>
[GlobalClass]
public partial class ModifyDamageAction : GameAction
{
    [Export] public int Delta { get; set; }

    protected override void Apply(Context ctx)
    {
        ctx.DamageModifier += Delta;
    }

    // 一次性修饰，无持久状态，不需要 Revert
}
