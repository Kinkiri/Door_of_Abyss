using Godot;

/// <summary>
/// 对目标单位施加 Buff。
/// Buff 的持续回合、属性修正、被动效果等由引用的 BuffData Resource 定义。
/// </summary>
[GlobalClass]
public partial class ApplyBuffAction : GameAction
{
    [Export] public BuffData BuffData { get; set; }

    /// <summary>初始叠层数（新建 Buff 时的 StackCount），默认 1</summary>
    [Export] public int InitialStacks { get; set; } = 1;

    protected override void Apply(Context ctx)
    {
        GD.Print($"[ApplyBuffAction] target={ctx.TargetUnit?.UnitData?.UnitName} buff={BuffData?.BuffName} stacks={InitialStacks}");
        if (ctx.TargetUnit != null && BuffData != null)
        {
            BuffManager.Instance?.ApplyBuff(ctx.TargetUnit, BuffData, ctx.SourceUnit, InitialStacks);
        }
    }
}
