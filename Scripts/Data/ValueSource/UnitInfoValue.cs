using Godot;

/// <summary>单位信息取值类型（可数值化的单位属性，返回枚举数值配合 CompareCondition 做判断）</summary>
public enum UnitInfoType
{
    /// <summary>单位类型（UnitType 枚举数值）</summary>
    Type,

    /// <summary>单位阵营（Team 枚举数值）</summary>
    Team,
}

/// <summary>
/// 单位信息值源，从 Context 的来源/目标/事件另一方单位读取单位类型、阵营等枚举信息。
/// 与 CardInfoValue 同模式：Info 指定取值类型，单位不存在时返回 DefaultValue。
/// </summary>
[GlobalClass]
public partial class UnitInfoValue : ValueSource
{
    /// <summary>读取哪个单位：Target=目标，Source=来源，EventOther=事件另一方（死亡事件=死者）</summary>
    [Export] public ValueTarget Unit { get; set; } = ValueTarget.Target;

    [Export] public UnitInfoType Info { get; set; } = UnitInfoType.Type;

    /// <summary>单位不存在时的默认返回值</summary>
    [Export] public int DefaultValue { get; set; } = 0;

    public override int GetValue(Context ctx)
    {
        var unit = Unit switch
        {
            ValueTarget.Source => ctx.SourceUnit,
            ValueTarget.Target => ctx.TargetUnit,
            ValueTarget.EventOther => ctx.EventOtherUnit,
            _ => null,
        };
        if (unit == null) return DefaultValue;

        return Info switch
        {
            UnitInfoType.Type => (int)unit.Type,
            UnitInfoType.Team => (int)unit.Team,
            _ => DefaultValue,
        };
    }
}
