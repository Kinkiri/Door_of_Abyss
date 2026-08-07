using Godot;

/// <summary>
/// 循环动作：重复执行子动作 N 次。
/// 循环次数由 ValueSource 动态决定，MaxIterations 为硬上限防止死循环。
/// 执行路径：
/// - **ActionQueue 队列**：展开为逐条队列（每次迭代均分 AnimationDuration 节奏，见 ActionQueue 展开分支）
/// - **被动/环境直发**：同步循环执行（不走队列节奏）
/// AnimationDuration 语义 = 整个循环的总展示时长（队列路径每次迭代间隔 = AnimationDuration / 次数）；
/// 内层子动作自身的 AnimationDuration 在队列路径被忽略。
/// </summary>
[GlobalClass]
public partial class RepeatAction : GameAction
{
    /// <summary>循环次数（值源）</summary>
    [Export] public ValueSource Times { get; set; }

    /// <summary>最大循环次数硬上限，默认 999</summary>
    [Export] public int MaxIterations { get; set; } = 999;

    /// <summary>每次循环执行的动作序列</summary>
    [Export] public GameAction[] Actions { get; set; }

    protected override void Apply(Context ctx)
    {
        int count = System.Math.Min(Times?.GetValue(ctx) ?? 0, MaxIterations);
        if (count <= 0 || Actions == null) return;

        GD.Print($"[RepeatAction] 循环 {count} 次 (上限 {MaxIterations})");
        for (int i = 0; i < count; i++)
        {
            foreach (var a in Actions)
                a?.Execute(ctx);
        }
    }
}
