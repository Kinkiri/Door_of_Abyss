using Godot;

/// <summary>
/// 修改目标单位上指定 Buff 的剩余回合数或叠层。
/// 若结果会变为负数则拒绝修改。减到 0 时自动移除 Buff。
/// </summary>
[GlobalClass]
public partial class ModifyBuffAction : GameAction
{
    /// <summary>目标 Buff 的 BuffID</summary>
    [Export] public string BuffID { get; set; } = "";

    /// <summary>回合变动量（正=增加，负=减少）</summary>
    [Export] public int TurnsDelta { get; set; }

    /// <summary>叠层变动量（正=增加，负=减少）</summary>
    [Export] public int StacksDelta { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetUnit == null) return;
        if (string.IsNullOrEmpty(BuffID)) return;

        var buff = BuffManager.Instance?.GetBuff(ctx.TargetUnit, BuffID);
        if (buff == null)
        {
            GD.Print($"[ModifyBuffAction] 未找到 Buff: {BuffID} 于 {ctx.TargetUnit.UnitData?.UnitName}");
            return;
        }

        // 验证：结果不能为负数
        int newTurns = buff.RemainingTurns + TurnsDelta;
        int newStacks = buff.StackCount + StacksDelta;

        if (newTurns < 0 || newStacks < 0)
        {
            GD.Print($"[ModifyBuffAction] 拒绝修改：{BuffID} 回合={buff.RemainingTurns}{TurnsDelta:+0;-0}={newTurns} " +
                     $"叠层={buff.StackCount}{StacksDelta:+0;-0}={newStacks}，结果为负");
            return;
        }

        // 应用修改：减层时逐层还原，加层时逐层施加
        if (StacksDelta < 0)
        {
            var revertCtx = new Context { TargetUnit = ctx.TargetUnit };
            for (int i = 0; i < -StacksDelta; i++)
                foreach (var action in buff.Data.OnApplyActions)
                    action.Revert(revertCtx);
        }
        else if (StacksDelta > 0)
        {
            var execCtx = new Context { TargetUnit = ctx.TargetUnit, SourceUnit = ctx.SourceUnit };
            for (int i = 0; i < StacksDelta; i++)
                foreach (var action in buff.Data.OnApplyActions)
                    action.Execute(execCtx);
        }

        if (TurnsDelta != 0)
            buff.RemainingTurns = newTurns;
        if (StacksDelta != 0)
            buff.StackCount = newStacks;

        GD.Print($"[ModifyBuffAction] {BuffID} 回合={buff.RemainingTurns} 叠层={buff.StackCount}");

        // 归零则移除
        if (buff.RemainingTurns <= 0 || buff.StackCount <= 0)
        {
            GD.Print($"[ModifyBuffAction] {BuffID} 归零，移除");
            BuffManager.Instance?.RemoveBuff(ctx.TargetUnit, buff);
        }
    }
}
