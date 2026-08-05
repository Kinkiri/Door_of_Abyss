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
        GD.Print($"[BuffInfoValue] Info={Info}, BuffID={BuffID}, ctx.SourceBuffID={ctx?.SourceBuffID}");
        if (Info == BuffInfoType.StackChanged && ctx.SourceBuffID == BuffID)
            return ctx.BuffChangedStacks;

        var unit = Unit switch
        {
            ValueTarget.Source => ctx.SourceUnit,
            ValueTarget.Target => ctx.TargetUnit,
            ValueTarget.EventOther => ctx.EventOtherUnit,
            _ => null,
        };
        if (unit == null) return DefaultValue;

        var buff = BuffManager.Instance?.GetBuff(unit, BuffID);
        if (buff == null) return DefaultValue;

        return Info switch
        {
            BuffInfoType.StackCount => buff.StackCount,
            BuffInfoType.RemainingTurns => buff.RemainingTurns,
            BuffInfoType.StackChanged => ctx.BuffChangedStacks,
            _ => DefaultValue,
        };
    }
}
