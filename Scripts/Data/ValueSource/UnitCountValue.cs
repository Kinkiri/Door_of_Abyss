using Godot;
using System.Linq;

/// <summary>统计阵营</summary>
public enum UnitCountTeam { All, Player, Enemy }

/// <summary>
/// 场上单位数量值源，可指定阵营和单位类型过滤。
/// </summary>
[GlobalClass]
public partial class UnitCountValue : ValueSource
{
    /// <summary>统计阵营。注意和 Unit.Team（Team 枚举）区分</summary>
    [Export] public UnitCountTeam FilterTeam { get; set; } = UnitCountTeam.All;

    /// <summary>true=只统计存活单位</summary>
    [Export] public bool OnlyAlive { get; set; } = true;

    /// <summary>true=包含门</summary>
    [Export] public bool IncludeDoor { get; set; } = false;

    public override int GetValue(Context ctx)
    {
        var units = UnitManager.Instance?.ActiveUnits;
        if (units == null) return 0;

        return units.Count(u =>
        {
            if (OnlyAlive && (!u.IsAlive || u.IsDead)) return false;
            if (!IncludeDoor && u.UnitData?.Type == UnitType.门) return false;
            if (FilterTeam == UnitCountTeam.Player && u.Team != Team.Player) return false;
            if (FilterTeam == UnitCountTeam.Enemy && u.Team != Team.Enemy) return false;
            return true;
        });
    }
}
