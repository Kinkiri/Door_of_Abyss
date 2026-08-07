using Godot;
using System;

/// <summary>
/// 单位视觉实体，跟随 Unit 数据更新位置和属性显示。
/// 内建动画：召唤入场、受伤闪红、治疗闪绿、死亡消散、移动着陆、Buff 弹跳。
/// 所有动画参数已 Export，可在预制体的 Inspector 中修改。
/// </summary>
public partial class UnitView : Node2D
{
    [Export] public UnitData UnitData { get; set; }
    public Unit Unit { get; set; }
    [Export] public Label NameLabel;
    [Export] public Label HPLabel;
    [Export] public Label ATKLabel;
    [Export] public Label APLabel;
    [Export] public ColorRect EnemyIndicator { get; set; }

    // ── 动画导出参数 ──────────────────────────────────────────────────────
    [ExportGroup("受伤")]
    [Export] public Color DamageColor = new Color(1, 0.2f, 0.2f);
    [Export] public float DamageFlashDuration = 0.12f;

    [ExportGroup("治疗")]
    [Export] public Color HealColor = new Color(0.2f, 1, 0.2f);
    [Export] public float HealFlashDuration = 0.12f;

    [ExportGroup("Buff")]
    [Export] public float BuffBounceScale = 1.25f;
    [Export] public float BuffBounceDuration = 0.25f;

    [ExportGroup("召唤")]
    [Export] public float SummonScaleDuration = 0.3f;

    [ExportGroup("死亡")]
    [Export] public float DeathFadeDuration = 0.35f;

    [ExportGroup("移动")]
    [Export] public float MoveBounceDuration = 0.3f;

    [ExportGroup("攻击")]
    [Export] public Color AttackFlashColor = new Color(1.5f, 1.5f, 1.5f);
    [Export] public float AttackFlashIn = 0.05f;
    [Export] public float AttackFlashOut = 0.08f;

    // ── 追踪状态 ──────────────────────────────────────────────────────────
    private int _prevHP;
    private Vector2I _prevGridPos;
    private bool _firstUpdate = true;
    private bool _isDying = false;

    public override void _Ready()
    {
        if (Unit == null) { QueueFree(); return; }
        if (UnitData == null) UnitData = Unit.UnitData;

        if (EnemyIndicator != null) EnemyIndicator.Visible = Unit.Team == Team.Enemy;
        if (NameLabel != null && Unit.Team == Team.Enemy) NameLabel.Modulate = Colors.Red;

        // 召唤入场：从 0 弹入
        Scale = Vector2.Zero;
        var summon = CreateTween();
        summon.SetTrans(Tween.TransitionType.Back);
        summon.SetEase(Tween.EaseType.Out);
        summon.TweenProperty(this, "scale", Vector2.One, SummonScaleDuration);

        // 订阅 ActionQueue 事件（攻击闪白、Buff 弹跳）
        ActionQueue.OnActionExecuted += OnActionExecuted;

        Unit.OnUnitUpdate += UpdateView;
        UpdateView();
        _firstUpdate = false;
    }

    public override void _ExitTree()
    {
        ActionQueue.OnActionExecuted -= OnActionExecuted;
        if (Unit != null) Unit.OnUnitUpdate -= UpdateView;
    }

    /// <summary>响应 ActionQueue：自己是攻击者时闪白，自己被施加/移除 Buff 或装备时弹跳</summary>
    private void OnActionExecuted(GameAction action, Context ctx)
    {
        if (action == null || _isDying || Unit == null) return;

        switch (action)
        {
            case DamageAction when ctx.SourceUnit == Unit:
                // 攻击者闪白
                var flash = CreateTween();
                flash.TweenProperty(this, "modulate", AttackFlashColor, AttackFlashIn);
                flash.TweenProperty(this, "modulate", Colors.White, AttackFlashOut);
                break;

            case ApplyBuffAction or RemoveBuffAction or EquipAction:
                // 检查自己是否在目标中
                if (ctx.TargetUnits != null)
                {
                    foreach (var t in ctx.TargetUnits)
                    {
                        if (t == Unit)
                        {
                            PlayBuffBounce();
                            break;
                        }
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 变身后刷新模板引用与显示（UnitView 在 _Ready 缓存了 UnitData，
    /// 变身改变 Unit.UnitData 后必须调用此方法才能显示新名字/属性/描述）。
    /// 同时清理自身挂载的 Buff/装备图标——变身语义=全清，视图自管清理
    /// （不依赖 UnitViewManager 的事件链路字典，RefreshUnitData 必定执行故必生效）。
    /// </summary>
    public void RefreshUnitData()
    {
        GD.Print($"[Transform][View] RefreshUnitData 执行: 新模板={Unit?.UnitData?.UnitName}");
        UnitData = Unit.UnitData;
        ClearUnitIcons();
        UpdateView();
    }

    /// <summary>清空自身挂载的 Buff/装备图标（BuffContainer/EquipmentContainer 子节点；装备无容器时挂视图根）</summary>
    private void ClearUnitIcons()
    {
        var buffContainer = FindChild("BuffContainer", true, false);
        GD.Print($"[Transform][View] ClearUnitIcons: BuffContainer={buffContainer?.Name ?? "null"} " +
                 $"子节点数={buffContainer?.GetChildCount() ?? 0}");
        if (buffContainer != null)
        {
            foreach (var child in buffContainer.GetChildren())
            {
                GD.Print($"[Transform][View]   QueueFree BuffContainer 子节点: {child.GetType().Name} {child.Name}");
                child.QueueFree();
            }
        }

        var equipContainer = FindChild("EquipmentContainer", true, false);
        if (equipContainer != null)
        {
            GD.Print($"[Transform][View] QueueFree EquipmentContainer 子节点: {equipContainer.GetChildCount()} 个");
            foreach (var child in equipContainer.GetChildren())
                child.QueueFree();
        }

        // 装备无容器时挂在视图根（固定位置避让 Buff 图标）
        foreach (var child in GetChildren())
        {
            if (child is EquipmentView)
            {
                GD.Print($"[Transform][View] QueueFree 视图根 EquipmentView: {child.Name}");
                child.QueueFree();
            }
        }
    }

    /// <summary>
    /// Unit 数据变化时调用：更新标签 + 检测 HP/位置变化触发动画。
    /// 死亡检测也在此完成（UnitManager.RemoveUnit 设置 IsDead 后调用 UpdateUnit），
    /// 替代原先 _Process 的每帧轮询。
    /// </summary>
    public void UpdateView()
    {
        if (UnitData == null) return;

        // 死亡检测：触发一次死亡动画，之后不再更新
        if (!_isDying && (Unit.IsDead || !Unit.IsAlive))
        {
            _isDying = true;
            PlayDeathAnimation();
            return;
        }

        // 标签更新
        if (NameLabel != null) NameLabel.Text = UnitData.UnitName;
        if (HPLabel != null) HPLabel.Text = $" {Unit.CurrentHP}/{Unit.MaxHP}";
        if (ATKLabel != null) ATKLabel.Text = $" {Unit.AttackPower}";
        if (APLabel != null) APLabel.Text = $" {Unit.ActionPoints}";

        // ── HP 变化检测 ─────────────────────────────────────────────
        if (!_firstUpdate && !_isDying)
        {
            int hpDiff = Unit.CurrentHP - _prevHP;
            if (hpDiff < 0)
                PlayDamageFlash(-hpDiff);
            else if (hpDiff > 0)
                PlayHealFlash(hpDiff);
        }
        _prevHP = Unit.CurrentHP;

        // ── 位置变化检测（死亡时不做）─────────────────────────────
        if (!_firstUpdate && !_isDying && Unit.GridPos != _prevGridPos)
            PlayMoveLanding();
        _prevGridPos = Unit.GridPos;
        Position = MapManager.Instance.GridToWorld(Unit.GridPos);
    }

    // ========================================================================
    // 动画方法
    // ========================================================================

    private void PlayDamageFlash(int amount)
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", DamageColor, DamageFlashDuration);
        tween.TweenProperty(this, "modulate", Colors.White, DamageFlashDuration * 0.5f);
    }

    private void PlayHealFlash(int amount)
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", HealColor, HealFlashDuration);
        tween.TweenProperty(this, "modulate", Colors.White, HealFlashDuration * 0.5f);
    }

    /// <summary>外部调用（ViewAnimator）：Buff 施加时弹跳</summary>
    public void PlayBuffBounce()
    {
        var bounce = CreateTween();
        bounce.TweenProperty(this, "scale", Vector2.One * BuffBounceScale, BuffBounceDuration * 0.5f);
        bounce.TweenProperty(this, "scale", Vector2.One, BuffBounceDuration * 0.5f);
    }

    private void PlayDeathAnimation()
    {
        var death = CreateTween();
        death.TweenProperty(this, "modulate", new Color(1, 0.2f, 0.2f, 1), DeathFadeDuration * 0.3f);
        death.TweenProperty(this, "scale", Vector2.Zero, DeathFadeDuration * 0.7f);
        death.Parallel().TweenProperty(this, "modulate:a", 0, DeathFadeDuration * 0.7f);
        death.TweenCallback(Callable.From(QueueFree));
    }

    private void PlayMoveLanding()
    {
        // 着陆弹跳：_Process 已经把 Position 同步到新位置，弹一下表示落地
        var bounce = CreateTween();
        bounce.SetTrans(Tween.TransitionType.Back);
        bounce.SetEase(Tween.EaseType.Out);
        bounce.TweenProperty(this, "scale", Vector2.One * 1.15f, MoveBounceDuration * 0.4f);
        bounce.TweenProperty(this, "scale", Vector2.One, MoveBounceDuration * 0.6f);
    }
}
