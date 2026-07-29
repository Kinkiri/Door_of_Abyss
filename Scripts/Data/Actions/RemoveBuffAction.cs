using Godot;

/// <summary>
/// 移除目标单位上指定 ID 的 Buff（驱散）。
/// 和 ModifyBuffAction(StacksDelta=-99) 不同，这是无条件整个移除，不关心层数。
/// </summary>
[GlobalClass]
public partial class RemoveBuffAction : GameAction
{
    /// <summary>要移除的 BuffID</summary>
    [Export] public string BuffID { get; set; } = "";

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null || string.IsNullOrEmpty(BuffID)) return;

        var buff = BuffManager.Instance?.GetBuff(ctx.TargetUnit, BuffID);
        if (buff == null)
        {
            GD.Print($"[RemoveBuffAction] 未找到 Buff: {BuffID}");
            return;
        }

        GD.Print($"[RemoveBuffAction] 移除 {BuffID} 于 {ctx.TargetUnit.UnitData?.UnitName}");
        BuffManager.Instance.RemoveBuff(ctx.TargetUnit, buff);
    }
}
