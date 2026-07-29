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

        var atkPositions = PathFinder.GetAttackableTargets(
            ctx.SourceUnit.GridPos, ctx.SourceUnit.AttackDistance,
            ctx.SourceUnit.Team, map);

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
            int dealt = UnitManager.Instance.DamageUnit(nearest, ctx.SourceUnit.AttackPower);
            if (dealt > 0)
            {
                GD.Print($"[AutoAttackAction] {ctx.SourceUnit.UnitData?.UnitName} " +
                         $"自动攻击 {nearest.UnitData?.UnitName} 造成 {dealt} 点伤害");

                EventBus.Instance?.Fire(EventType.OnDealDamage,
                    new Context { TargetUnit = nearest }, subject: ctx.SourceUnit);
                EventBus.Instance?.Fire(EventType.OnTakeDamage,
                    new Context { TargetUnit = ctx.SourceUnit }, subject: nearest);
                if (!nearest.IsAlive)
                    EventBus.Instance?.Fire(EventType.OnKill,
                        new Context { TargetUnit = nearest }, subject: ctx.SourceUnit);
            }
        }
    }
}
