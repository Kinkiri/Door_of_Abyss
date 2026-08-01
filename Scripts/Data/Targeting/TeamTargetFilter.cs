using Godot;

// 属性名 Team 会遮蔽 Team 枚举类型名，用别名引用枚举
using TeamEnum = Team;

/// <summary>
/// 阵营筛选器：按相对阵营（相对效果来源）过滤候选单位。
/// </summary>
[GlobalClass]
public partial class TeamTargetFilter : PropertyTargetFilter
{
    /// <summary>相对阵营过滤（相对效果来源）</summary>
    [Export] public TeamFilter Team { get; set; } = TeamFilter.All;

    public override bool IsUnitMatch(Unit unit, Team sourceTeam, Context ctx)
    {
        if (!TargetResolver.IsValidTarget(unit)) return false;

        // 相对阵营（保持旧 TargetResolver 语义：Enemy 取与 sourceTeam 相反的阵营，Neutral 不命中）
        if (Team == TeamFilter.Enemy)
        {
            TeamEnum enemyTeam = sourceTeam == TeamEnum.Player ? TeamEnum.Enemy : TeamEnum.Player;
            if (unit.Team != enemyTeam) return false;
        }
        else if (Team == TeamFilter.Ally)
        {
            if (unit.Team != sourceTeam) return false;
        }
        return true;
    }

    public override TeamFilter GetTeamFilter() => Team;
}
