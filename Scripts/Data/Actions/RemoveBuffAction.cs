using Godot;

/// <summary>
/// 移除目标单位上指定 ID 的 Buff（驱散）。
/// 和 ModifyBuffAction(StacksDelta=-99) 不同，这是无条件整个移除，不关心层数。
/// 支持多目标：TargetUnits 优先、TargetUnit 兜底（同 ModifyStatAction 约定——
/// EventBus filter 路径把目标放 TargetUnits 并把 TargetUnit 置 null，两路都兼容）。
/// </summary>
[GlobalClass]
public partial class RemoveBuffAction : GameAction
{
    /// <summary>要移除的 BuffID</summary>
    [Export] public string BuffID { get; set; } = "";

    protected override void Apply(Context ctx)
    {
        if (string.IsNullOrEmpty(BuffID)) return;

        var targets = (ctx.TargetUnits != null && ctx.TargetUnits.Length > 0)
            ? ctx.TargetUnits
            : new[] { ctx.TargetUnit };
        if (targets == null) return;

        foreach (var unit in targets)
        {
            if (unit == null || unit.IsDead) continue;

            var buff = BuffManager.Instance?.GetBuff(unit, BuffID);
            if (buff == null)
            {
                GD.Print($"[RemoveBuffAction] 未找到 Buff: {BuffID}");
                continue;
            }
            if (buff.Data.CanBeChanged == false)
            {
                GD.Print($"[RemoveBuffAction] Buff: {BuffID} 不可移除");
                continue;
            }
            GD.Print($"[RemoveBuffAction] 移除 {BuffID} 于 {unit.UnitData?.UnitName}");
            BuffManager.Instance.RemoveBuff(unit, buff);
        }
    }
}
