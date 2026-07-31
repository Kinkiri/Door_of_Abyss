using Godot;

/// <summary>
/// 修改目标单位上指定 Buff 的剩余回合数或叠层。
/// 回合/叠层最小减到 0（归零时自动移除 Buff）。
/// RemainingTurns = -1 表示永久 Buff，回合修改被忽略。
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

        // ── 回合数：先检测初始值，再修改，最小减到 0 ──
        // RemainingTurns 合法取值：-1 = 永久（哨兵值），N>=0 = 剩余回合数
        int newTurns = buff.RemainingTurns;
        if (buff.RemainingTurns == -1)
        {
            // 永久 Buff：回合不参与修改
            if (TurnsDelta != 0)
                GD.Print($"[ModifyBuffAction] 警告: {BuffID} 为永久 Buff（RemainingTurns=-1），回合修改 {TurnsDelta:+0;-0} 被忽略");
        }
        else
        {
            int baseTurns = buff.RemainingTurns;
            if (baseTurns < -1)
            {
                // 非法负值：警告并按 0 处理
                GD.Print($"[ModifyBuffAction] 警告: {BuffID} RemainingTurns={baseTurns} 为非法负值，按 0 处理");
                baseTurns = 0;
            }
            newTurns = System.Math.Max(0, baseTurns + TurnsDelta);
        }

        // ── 叠层：最小减到 0（归零触发移除），不能为负 ──
        int newStacks = System.Math.Max(0, buff.StackCount + StacksDelta);

        // ── 应用修改：减层逐层还原（最多还原到 0 层），加层逐层施加 ──
        if (StacksDelta < 0)
        {
            var revertCtx = new Context { TargetUnit = ctx.TargetUnit };
            int reduceCount = System.Math.Min(-StacksDelta, buff.StackCount);
            if (buff.Data.OnApplyActions != null)
                for (int i = 0; i < reduceCount; i++)
                    foreach (var action in buff.Data.OnApplyActions)
                        action.Revert(revertCtx);
        }
        else if (StacksDelta > 0)
        {
            var execCtx = new Context { TargetUnit = ctx.TargetUnit, SourceUnit = ctx.SourceUnit };
            if (buff.Data.OnApplyActions != null)
                for (int i = 0; i < StacksDelta; i++)
                    foreach (var action in buff.Data.OnApplyActions)
                        action.Execute(execCtx);
        }

        if (newTurns != buff.RemainingTurns)
            buff.RemainingTurns = newTurns;
        if (newStacks != buff.StackCount)
            buff.StackCount = newStacks;

        GD.Print($"[ModifyBuffAction] {BuffID} 回合={buff.RemainingTurns} 叠层={buff.StackCount}");

        // 归零则移除：永久 Buff（RemainingTurns=-1）只按叠层判断
        bool turnsExpired = buff.RemainingTurns != -1 && buff.RemainingTurns <= 0;
        if (turnsExpired || buff.StackCount <= 0)
        {
            GD.Print($"[ModifyBuffAction] {BuffID} 归零，移除");
            BuffManager.Instance?.RemoveBuff(ctx.TargetUnit, buff);
        }
    }
}
