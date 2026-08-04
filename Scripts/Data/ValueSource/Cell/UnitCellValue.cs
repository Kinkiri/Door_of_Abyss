using Godot;

/// <summary>
/// 单位坐标值源：读取 Context 中来源/目标/事件另一方单位的格子坐标（GridPos）。
/// </summary>
[GlobalClass]
public partial class UnitCellValue : CellValueSource
{
    /// <summary>读取哪个单位：Source=来源，Target=目标，EventTarget=事件另一方（死亡事件=死者）</summary>
    [Export] public ValueTarget Unit { get; set; } = ValueTarget.Target;

    public override Vector2I? GetCell(Context ctx)
    {
        var unit = Unit switch
        {
            ValueTarget.Source => ctx.SourceUnit,
            ValueTarget.Target => ctx.TargetUnit,
            ValueTarget.EventTarget => ctx.EventTargetUnit,
            _ => null,
        };
        return unit?.GridPos;
    }
}
