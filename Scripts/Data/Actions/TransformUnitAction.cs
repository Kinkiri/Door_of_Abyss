using Godot;

/// <summary>
/// 单位变身：将目标单位切换为指定模板并完全重置（清 buff/装备 + 换被动订阅）。
/// 配置 UnitData（Resource 引用）或 UnitID（字符串查 UnitLibrary，避免循环引用）均可。
/// 变身保留位置与阵营；当前血量/属性按新模板重置为满值；生效中的 buff/装备全部清除。
/// </summary>
[GlobalClass]
public partial class TransformUnitAction : GameAction
{
    /// <summary>目标模板：直接指定（null = 走 UnitID 路径）</summary>
    [Export] public UnitData UnitData { get; set; }

    /// <summary>目标模板：按 UnitID 从 UnitLibrary 查（循环引用等场景用字符串规避）</summary>
    [Export] public string UnitID { get; set; } = "";

    protected override void Apply(Context ctx)
    {
        var targets = (ctx.TargetUnits != null && ctx.TargetUnits.Length > 0)
            ? ctx.TargetUnits
            : (ctx.TargetUnit != null ? new[] { ctx.TargetUnit } : null);
        if (targets == null) return;

        var newData = UnitData
            ?? (string.IsNullOrEmpty(UnitID) ? null : UnitLibrary.GetUnitByID(UnitID));
        if (newData == null)
        {
            GD.PrintErr("[TransformUnitAction] 未配置 UnitData/UnitID，无法变身");
            return;
        }

        foreach (var unit in targets)
        {
            if (unit == null || unit.IsDead) continue;
            UnitManager.Instance?.TransformUnit(unit, newData);
        }
    }
}
