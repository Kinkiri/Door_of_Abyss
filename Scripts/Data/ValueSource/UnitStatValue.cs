using Godot;

/// <summary>
/// 单位属性值源，从 Context 中的来源或目标单位读取指定属性。
/// </summary>
[GlobalClass]
public partial class UnitStatValue : ValueSource
{
    [Export] public ValueTarget Unit { get; set; } = ValueTarget.Target;

    [Export] public ModifyStatType Stat { get; set; } = ModifyStatType.AttackPower;

    /// <summary>true=取 CurrentHP，false=取 MaxHP（仅对 MaxHP 类型有效）</summary>
    [Export] public bool CurrentHP { get; set; } = true;

    public override int GetValue(Context ctx)
    {
        var unit = Unit == ValueTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null) return 0;

        return Stat switch
        {
            ModifyStatType.AttackPower => unit.AttackPower,
            ModifyStatType.MaxHP => CurrentHP ? unit.CurrentHP : unit.MaxHP,
            ModifyStatType.Stamina => unit.MaxStamina,
            ModifyStatType.AttackDistance => unit.AttackDistance,
            ModifyStatType.ActionPoints => unit.ActionPoints,
            _ => 0,
        };
    }
}
