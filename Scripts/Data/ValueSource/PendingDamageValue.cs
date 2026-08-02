using Godot;

/// <summary>
/// 本次伤害基础伤害值源：读取 ctx.PendingDamage（攻击前/受击前事件时由 DamageAction 填充）。
/// 配合条件使用："本次伤害 ≥ 目标当前 HP" 判断是否会致死（致命免伤）。
/// </summary>
[GlobalClass]
public partial class PendingDamageValue : ValueSource
{
    public override int GetValue(Context ctx) => ctx.PendingDamage;
}
