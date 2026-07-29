using Godot;

/// <summary>
/// 通用效果基类。所有具体动作继承此类，通过多态分发。
/// 编辑器可创建任意子类 Resource，放入 CardData/EffectData/BuffData 的 Actions[] 数组。
/// </summary>
[GlobalClass]
public abstract partial class GameAction : Resource
{
    /// <summary>此动作的动画播放时长（秒），供 ActionQueue 控制节奏。策划可单独配置每个动作子类</summary>
    [Export] public float AnimationDuration { get; set; } = 0.5f;

    /// <summary>
    /// 执行动作。基类模板方法——先解析目标，再委托给子类的 Apply()。
    /// </summary>
    public void Execute(Context ctx)
    {
        GD.Print($"[Action] {GetType().Name} 目标={ctx.TargetUnit?.UnitData?.UnitName} 格子={ctx.TargetCell?.GridPos}");
        ResolveTargets(ctx);
        Apply(ctx);
    }

    /// <summary>
    /// 还原动作（用于 Buff 到期时撤销属性修改）。
    /// 不可逆动作（Damage/Heal 等）无需重写。
    /// </summary>
    public virtual void Revert(Context ctx) { }

    /// <summary>
    /// 目标扩散：根据 SourceCard 的 TargetShape + TargetFilter 将单目标扩展为多目标数组。
    /// 子类 Apply() 执行时 ctx.TargetUnits 已就绪。
    /// </summary>
    private static void ResolveTargets(Context ctx)
    {
        // 卡牌路径：按卡牌定义的 Shape+Filter 扩散
        if (ctx.SourceCard != null && (ctx.TargetUnits == null || ctx.TargetUnits.Length == 0))
        {
            ctx.TargetUnits = TargetResolver.Resolve(
                ctx.SourceCard.Shape, ctx.SourceCard.Filter,
                ctx.SourceUnit, ctx.TargetUnit, ctx.TargetCell,
                ctx.SourceTeam, ctx.SourceCard.CardData?.AreaRange ?? 1);
        }

        // 被动/非卡牌路径：单目标包装为数组
        if ((ctx.TargetUnits == null || ctx.TargetUnits.Length == 0) && ctx.TargetUnit != null)
            ctx.TargetUnits = new[] { ctx.TargetUnit };
    }

    /// <summary>
    /// 子类实现的具体效果逻辑。
    /// 此时 ctx.TargetUnits 已按卡牌 Shape/Filter 扩散完毕，可直接使用。
    /// </summary>
    protected abstract void Apply(Context ctx);
}
