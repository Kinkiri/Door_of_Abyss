using Godot;
using System.Linq;

/// <summary>
/// 随机变身：从模板库中按 CardFilter 筛选匹配的单位卡，随机取一张（按卡随机、不去重——
/// 同单位多张卡被选中的权重更高），变身成其单位。如"随机变为费用≤6的擢升之手单位"：
/// Filters = [CardFactionFilter{擢升之手}, CardCostFilter{MaxCost=6}]。
/// 筛选条件与模板库检索复用 CardFilter/CardLibrary 通用机制，与动作解耦。
/// </summary>
[GlobalClass]
public partial class RandomTransformAction : GameAction
{
    /// <summary>卡牌模板筛选（数组默认 And；null/空 = 全部卡中随机，慎用）</summary>
    [Export] public CardFilter[] Filters { get; set; }

    protected override void Apply(Context ctx)
    {
        var targets = (ctx.TargetUnits != null && ctx.TargetUnits.Length > 0)
            ? ctx.TargetUnits
            : (ctx.TargetUnit != null ? new[] { ctx.TargetUnit } : null);
        if (targets == null) return;

        var filter = CardFilter.CombineAnd(Filters);

        // 候选 = 匹配筛选的单位卡模板（按卡随机，不去重）
        var pool = CardLibrary.GetCards(filter)
            .OfType<UnitCardData>()
            .Where(c => c.UnitData != null)
            .ToArray();
        if (pool.Length == 0)
        {
            GD.PrintErr("[RandomTransformAction] 筛选无匹配的单位卡，无法变身");
            return;
        }

        foreach (var unit in targets)
        {
            if (unit == null || unit.IsDead) continue;
            var picked = pool[GD.Randi() % pool.Length];
            UnitManager.Instance?.TransformUnit(unit, picked.UnitData);
            GD.Print($"[RandomTransformAction] 随机变身: {unit.UnitData?.UnitName} → {picked.UnitData.UnitName}" +
                     $"（{picked.CardID} 费用{picked.Cost}）");
        }
    }
}
