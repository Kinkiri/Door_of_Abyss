using Godot;

/// <summary>
/// 非条件：子条件不通过才算通过。
/// </summary>
[GlobalClass]
public partial class NotCondition : Condition
{
    [Export] public Condition Condition { get; set; }

    public override bool IsMet(Context ctx)
    {
        if (Condition == null) return true;
        return !Condition.IsMet(ctx);
    }
}
