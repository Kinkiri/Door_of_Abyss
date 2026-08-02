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

    /// <summary>动态值源，设置后覆盖 Value</summary>
    [Export] public ValueSource ValueSource { get; set; }

    protected override void Apply(Context ctx)
    {
        InitialStacks = ValueSource?.GetValue(ctx) ?? InitialStacks;
        if (ctx.TargetUnits == null) return;
        GD.Print($"[ApplyBuffAction] targets={ctx.TargetUnits.Length} buff={BuffData?.BuffName} stacks={InitialStacks}");
        foreach (var target in ctx.TargetUnits)
        {
            if (target != null && BuffData != null)
                BuffManager.Instance?.ApplyBuff(target, BuffData, ctx.SourceUnit, InitialStacks);
        }
    }
}
