using Godot;

/// <summary>
/// 移除目标格子上指定 ID 的环境（驱散）。
/// 无条件整个移除，不关心持续时间；属性修正自动还原 + 取消被动订阅。
/// </summary>
[GlobalClass]
public partial class RemoveEnvironmentAction : GameAction
{
    /// <summary>要移除的环境 ID（留空=移除目标格子上任意环境）</summary>
    [Export] public string EnvironmentID { get; set; } = "";

    protected override void Apply(Context ctx)
    {
        if (ctx.TargetCells == null || ctx.TargetCells.Length == 0) return;

        foreach (var cell in ctx.TargetCells)
        {
            if (cell == null) continue;
            if (string.IsNullOrEmpty(EnvironmentID))
            {
                if (cell.Environment != null)
                    EnvironmentManager.Instance?.RemoveEnvironment(cell);
            }
            else
            {
                EnvironmentManager.Instance?.RemoveEnvironmentByData(cell, EnvironmentID);
            }
        }
    }
}
