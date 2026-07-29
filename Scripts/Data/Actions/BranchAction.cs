using Godot;

/// <summary>
/// 分支动作：检查条件，满足时执行 ThenActions，否则执行 ElseActions。
/// 可嵌入在任意 Action 序列中，支持多层嵌套。
/// </summary>
[GlobalClass]
public partial class BranchAction : GameAction
{
    /// <summary>判断条件</summary>
    [Export] public Condition Condition { get; set; }

    /// <summary>条件为真时执行</summary>
    [Export] public GameAction[] ThenActions { get; set; }

    /// <summary>条件为假时执行（可选）</summary>
    [Export] public GameAction[] ElseActions { get; set; }

    protected override void Apply(Context ctx)
    {
        bool met = Condition?.IsMet(ctx) ?? true;
        var actions = met ? ThenActions : ElseActions;
        if (actions == null) return;

        GD.Print($"[BranchAction] 条件={(met ? "满足" : "不满足")} 执行 {(met ? "Then" : "Else")} ({actions.Length} 个动作)");
        foreach (var a in actions)
            a?.Execute(ctx);
    }
}
