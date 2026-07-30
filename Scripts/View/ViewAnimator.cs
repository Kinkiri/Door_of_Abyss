using Godot;
using System;

/// <summary>
/// 视觉动画层，订阅 ActionQueue.ActionStarted 信号，播放纯 Tween 动画。
/// 不修改任何 Manager/Data 逻辑，不影响核心架构。
/// 所有数值参数已导出，可在编辑器 Inspector 中修改。
/// </summary>
public partial class ViewAnimator : Node
{
    // ========== 伤害闪红 ==========
    [ExportGroup("伤害")]
    [Export] public Color DamageFlashColor = new Color(1, 0.2f, 0.2f);
    [Export] public float DamageFlashDuration = 0.12f;
    [Export] public bool ShowDamageNumbers = true;

    // ========== 治疗闪绿 ==========
    [ExportGroup("治疗")]
    [Export] public Color HealFlashColor = new Color(0.2f, 1, 0.2f);
    [Export] public float HealFlashDuration = 0.12f;
    [Export] public bool ShowHealNumbers = true;

    // ========== Buff 弹跳 ==========
    [ExportGroup("Buff")]
    [Export] public float BuffBounceScale = 1.25f;
    [Export] public float BuffBounceDuration = 0.25f;

    // ========== 召唤入场 ==========
    [ExportGroup("召唤")]
    [Export] public float SummonScaleDuration = 0.3f;

    // ========== 死亡消散 ==========
    [ExportGroup("死亡")]
    [Export] public float DeathFadeDuration = 0.35f;

    // ========== 浮动数字 ==========
    [ExportGroup("浮动数字")]
    [Export] public float FloatNumberDuration = 0.7f;
    [Export] public float FloatNumberRise = 28f;
    [Export] public int FloatNumberFontSize = 18;

    // ========== 移动 ==========
    [ExportGroup("移动")]
    [Export] public float MoveDuration = 0.3f;

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

        GD.Print($"[ViewAnimator] 收到 {action.GetType().Name}, TargetUnits={(ctx.TargetUnits?.Length.ToString() ?? "null")}, TargetUnit={ctx.TargetUnit?.UnitData?.UnitName}");

        switch (action)
        {
            case DamageAction dmg:
                PlayDamageAnimation(ctx, dmg);
                break;
            case HealAction heal:
                PlayHealAnimation(ctx, heal);
                break;
            case ApplyBuffAction:
                PlayBuffAnimation(ctx);
                break;
            case SummonUnitAction:
                PlaySummonAnimation(ctx);
                break;
            case RemoveBuffAction:
                PlayBuffAnimation(ctx);
                break;
            case MoveUnitAction:
                PlayMoveAnimation(ctx);
                break;
        }
    }

    // ========================================================================
    // 伤害
    // ========================================================================
    private void PlayDamageAnimation(Context ctx, DamageAction dmg)
    {
        var targets = ctx.TargetUnits;
        if (targets == null)
        {
            GD.Print("[ViewAnimator] 伤害动画跳过: TargetUnits=null");
            return;
        }

        int dmgValue = dmg.ValueSource != null ? dmg.ValueSource.GetValue(ctx) : dmg.Value;

        foreach (var unit in targets)
        {
            var view = UnitManager.Instance?.GetUnitView(unit);
            if (view == null)
            {
                GD.Print($"[ViewAnimator] 伤害动画跳过: 找不到 {unit?.UnitData?.UnitName} 的视图");
                continue;
            }

            GD.Print($"[ViewAnimator] 播放伤害动画: {unit.UnitData?.UnitName} value={dmgValue}");


            // 闪红
            var flash = CreateTween();
            flash.TweenProperty(view, "modulate", DamageFlashColor, DamageFlashDuration);
            flash.TweenProperty(view, "modulate", Colors.White, DamageFlashDuration * 0.5f);

            // 浮动数字
            if (ShowDamageNumbers && dmgValue > 0)
                ShowFloatingNumber(view, $"-{dmgValue}", DamageFlashColor);
        }
    }

    // ========================================================================
    // 治疗
    // ========================================================================
    private void PlayHealAnimation(Context ctx, HealAction heal)
    {
        var targets = ctx.TargetUnits;
        if (targets == null) return;

        int healValue = heal.ValueSource != null ? heal.ValueSource.GetValue(ctx) : heal.Value;

        foreach (var unit in targets)
        {
            var view = UnitManager.Instance?.GetUnitView(unit);
            if (view == null) continue;

            // 闪绿
            var flash = CreateTween();
            flash.TweenProperty(view, "modulate", HealFlashColor, HealFlashDuration);
            flash.TweenProperty(view, "modulate", Colors.White, HealFlashDuration * 0.5f);

            // 浮动数字
            if (ShowHealNumbers && healValue > 0)
                ShowFloatingNumber(view, $"+{healValue}", HealFlashColor);
        }
    }

    // ========================================================================
    // Buff（弹跳）
    // ========================================================================
    private void PlayBuffAnimation(Context ctx)
    {
        var targets = ctx.TargetUnits;
        if (targets == null) return;

        foreach (var unit in targets)
        {
            var view = UnitManager.Instance?.GetUnitView(unit);
            if (view == null) continue;

            var bounce = CreateTween();
            bounce.TweenProperty(view, "scale", Vector2.One * BuffBounceScale, BuffBounceDuration * 0.5f);
            bounce.TweenProperty(view, "scale", Vector2.One, BuffBounceDuration * 0.5f);
        }
    }

    // ========================================================================
    // 召唤（缩放入场）
    // ========================================================================
    private void PlaySummonAnimation(Context ctx)
    {
        var targets = ctx.TargetUnits;
        if (targets == null) return;

        foreach (var unit in targets)
        {
            var view = UnitManager.Instance?.GetUnitView(unit);
            if (view == null) continue;

            // _Ready 已经设好了位置，从 0 弹入
            view.Scale = Vector2.Zero;
            var summon = CreateTween();
            summon.SetTrans(Tween.TransitionType.Back);
            summon.SetEase(Tween.EaseType.Out);
            summon.TweenProperty(view, "scale", Vector2.One, SummonScaleDuration);
        }
    }

    // ========================================================================
    // 死亡（闪红 + 缩小）
    // ========================================================================
    public void PlayDeathAnimation(UnitView view)
    {
        if (view == null) return;

        var death = CreateTween();
        death.TweenProperty(view, "modulate", new Color(1, 0.2f, 0.2f, 1), DeathFadeDuration * 0.3f);
        death.TweenProperty(view, "scale", Vector2.Zero, DeathFadeDuration * 0.7f);
        death.Parallel().TweenProperty(view, "modulate:a", 0, DeathFadeDuration * 0.7f);
    }

    // ========================================================================
    // 移动（新位置着陆弹跳）
    // ========================================================================
    private void PlayMoveAnimation(Context ctx)
    {
        var unit = ctx.SourceUnit ?? ctx.TargetUnit;
        if (unit == null) return;
        var view = UnitManager.Instance?.GetUnitView(unit);
        if (view == null) return;

        var bounce = CreateTween();
        bounce.SetTrans(Tween.TransitionType.Back);
        bounce.SetEase(Tween.EaseType.Out);
        bounce.TweenProperty(view, "scale", Vector2.One * 1.15f, MoveDuration * 0.4f);
        bounce.TweenProperty(view, "scale", Vector2.One, MoveDuration * 0.6f);
    }

    // ========================================================================
    // 浮动数字
    // ========================================================================
    private void ShowFloatingNumber(Node2D parent, string text, Color color)
    {
        var label = new Label();
        label.Text = text;
        label.Modulate = color;
        label.AddThemeFontSizeOverride("font_size", FloatNumberFontSize);
        label.HorizontalAlignment = HorizontalAlignment.Center;

        // 挂在 UnitView 上方偏移位置
        label.Position = new Vector2(0, -20);

        parent.AddChild(label);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", label.Position + Vector2.Up * FloatNumberRise, FloatNumberDuration);
        tween.TweenProperty(label, "modulate:a", 0, FloatNumberDuration * 0.8f);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
