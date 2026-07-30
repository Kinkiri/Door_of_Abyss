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

        // 只处理 Buff 类动作，调用 UnitView 的弹跳动画
        if (action is ApplyBuffAction or RemoveBuffAction)
        {
            PlayBuffAnimations(ctx);
        }
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
