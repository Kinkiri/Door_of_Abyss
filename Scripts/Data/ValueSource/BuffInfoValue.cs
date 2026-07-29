using Godot;

/// <summary>
/// Buff 信息值源，从 Context 中的来源或目标单位上读取指定 Buff 的叠层或回合数。
/// </summary>
[GlobalClass]
public partial class BuffInfoValue : ValueSource
{
    [Export] public ValueTarget Unit { get; set; } = ValueTarget.Target;

    [Export] public string BuffID { get; set; } = "";

    [Export] public BuffInfoType Info { get; set; } = BuffInfoType.StackCount;

    /// <summary>找不到 Buff 时的默认返回值</summary>
    [Export] public int DefaultValue { get; set; } = 0;

    public override int GetValue(Context ctx)
    {
        var unit = Unit == ValueTarget.Target ? ctx.TargetUnit : ctx.SourceUnit;
        if (unit == null) return DefaultValue;

        var buff = BuffManager.Instance?.GetBuff(unit, BuffID);
        if (buff == null) return DefaultValue;

        return Info switch
        {
            BuffInfoType.StackCount => buff.StackCount,
            BuffInfoType.RemainingTurns => buff.RemainingTurns,
            _ => DefaultValue,
        };
    }
}
