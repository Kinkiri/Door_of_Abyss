using Godot;

/// <summary>
/// 自动攻击范围内最近的敌方单位。
/// 使用单位自身的攻击力（AttackPower）和攻击范围（AttackDistance），忽略 GameAction.Value。
/// </summary>
[GlobalClass]
public partial class AutoAttackAction : GameAction
{
    protected override void Apply(Context ctx)
    {
        if (ctx.SourceUnit == null) return;
        var map = MapManager.Instance?.Map;
        if (map == null) return;

        map.TryGetValue(ctx.SourceUnit.GridPos, out Cell srcCell);
        var atkCtx = new Context { SourceUnit = ctx.SourceUnit, Map = map, TargetCell = srcCell };
        var atkPositions = PathFinder.GetAttackableTargets(
            ctx.SourceUnit.GridPos, ctx.SourceUnit.AttackShape, ctx.SourceUnit.AttackDistance,
            ctx.SourceUnit.Team, map, atkCtx);

        Unit nearest = null;
        int nearestDist = int.MaxValue;
        Team enemyTeam = ctx.SourceUnit.Team == Team.Player ? Team.Enemy : Team.Player;

        foreach (var pos in atkPositions)
        {
            if (!map.TryGetValue(pos, out Cell c)) continue;
            var occupant = c.OccupyingUnit;
            if (occupant == null || occupant.Team != enemyTeam || !occupant.IsAlive) continue;

            int dist = Mathf.Abs(pos.X - ctx.SourceUnit.GridPos.X) +
                       Mathf.Abs(pos.Y - ctx.SourceUnit.GridPos.Y);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = occupant;
            }
        }

        if (nearest != null)
        {
            // 统一走 DamageAction 攻击链路：触发攻击前/受击前（加伤/减伤修饰）、
            // OnDealDamage / OnTakeDamage / OnKill 等战斗被动事件
            var dmgAction = new DamageAction { Value = ctx.SourceUnit.AttackPower };
            dmgAction.Execute(new Context
            {
                SourceUnit = ctx.SourceUnit,
                TargetUnits = new[] { nearest },
            });
        }
    }
}
