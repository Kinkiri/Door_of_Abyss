using Godot;
using System;

/// <summary>
/// 桥接 ActionQueue 与 UnitView 动画。
/// ActionQueue 执行每个动作后触发 OnActionExecuted，
/// 这里识别 Buff 类动作并调用 UnitView.PlayBuffBounce()。
/// UnitView 自身的 HP/位置/死亡动画由 UnitView._Process + UpdateView 自驱动。
/// </summary>
public partial class ViewAnimator : Node
{
    public override void _Ready()
    {
        ActionQueue.OnActionExecuted += OnActionExecuted;
    }

    public override void _ExitTree()
    {
        ActionQueue.OnActionExecuted -= OnActionExecuted;
    }

    private void OnActionExecuted(GameAction action, Context ctx)
    {
        if (action == null) return;

        switch (action)
        {
            case DamageAction:
                PlayAttackerFlash(ctx);
                break;
            case ApplyBuffAction or RemoveBuffAction:
                PlayBuffAnimations(ctx);
                break;
        }
    }

    /// <summary>攻击者闪白</summary>
    private void PlayAttackerFlash(Context ctx)
    {
        if (ctx.SourceUnit == null) return;
        var view = FindUnitView(ctx.SourceUnit);
        if (view == null) return;

        var flash = CreateTween();
        flash.TweenProperty(view, "modulate", new Color(1.5f, 1.5f, 1.5f), 0.05f);
        flash.TweenProperty(view, "modulate", Colors.White, 0.08f);
    }

    private void PlayBuffAnimations(Context ctx)
    {
        var targets = ctx.TargetUnits;
        if (targets == null) return;

        foreach (var unit in targets)
        {
            var view = FindUnitView(unit);
            view?.PlayBuffBounce();
        }
    }

    private static UnitView FindUnitView(Unit unit)
    {
        if (unit == null) return null;
        var view = UnitManager.Instance?.GetUnitView(unit);
        if (view != null) return view;
        var mapLayer = MapManager.Instance?.BaseMapLayer;
        if (mapLayer == null) return null;
        foreach (var child in mapLayer.GetChildren())
            if (child is UnitView uv && uv.Unit == unit)
                return uv;
        return null;
    }
}
