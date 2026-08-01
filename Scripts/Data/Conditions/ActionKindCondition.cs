using Godot;

/// <summary>
/// 行动类型条件：判断 OnUnitAct 事件的行动种类（移动/攻击）。
/// ctx.ActType 由 BattleManager 触发时填充。
/// </summary>
[GlobalClass]
public partial class ActionKindCondition : Condition
{
    [Export(PropertyHint.Enum, "无,移动,攻击")] public UnitActType Kind { get; set; } = UnitActType.Move;

    public override bool IsMet(Context ctx)
    {
        return ctx.ActType == Kind;
    }
}
