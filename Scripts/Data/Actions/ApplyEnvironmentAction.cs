using Godot;

/// <summary>
/// 对目标格子施加环境（目标为格子，TargetKind.Cell）。
/// 环境属性修正、持续回合、被动效果等由引用的 EnvironmentData Resource 定义。
/// 同格已有环境时先完整还原旧环境再替换（替换式覆盖）。
/// </summary>
[GlobalClass]
public partial class ApplyEnvironmentAction : GameAction
{
    [Export] public EnvironmentData EnvironmentData { get; set; }

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetCells == null || ctx.TargetCells.Length == 0) return;

        GD.Print($"[ApplyEnvironmentAction] targets={ctx.TargetCells.Length} environment={EnvironmentData?.EnvironmentName}");
        if (EnvironmentData == null) EnvironmentData = (ctx.SourceCard.CardData as EnvironmentCardData).EnvironmentData;
        foreach (var cell in ctx.TargetCells)
        {
            if (cell != null && EnvironmentData != null)
                EnvironmentManager.Instance?.ApplyEnvironment(cell, EnvironmentData, ctx.SourceUnit);
        }
    }
}
