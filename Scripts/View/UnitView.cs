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
    [Export] public Panel DescriptionPanel { get; set; }
    [Export] public Label DescriptionLabel { get; set; }
    [Export] public ColorRect EnemyIndicator { get; set; }

    // ── 动画导出参数 ──────────────────────────────────────────────────────
    [ExportGroup("受伤")]
    [Export] public Color DamageColor = new Color(1, 0.2f, 0.2f);
    [Export] public float DamageFlashDuration = 0.12f;
    [Export] public bool ShowDamageNumbers = true;

    [ExportGroup("治疗")]
    [Export] public Color HealColor = new Color(0.2f, 1, 0.2f);
    [Export] public float HealFlashDuration = 0.12f;
    [Export] public bool ShowHealNumbers = true;

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

    [ExportGroup("浮动数字")]
    [Export] public PackedScene FloatingNumberPrefab { get; set; }
    [Export] public float FloatLifetime = 0.7f;
    [Export] public float FloatRise = 28f;
    [Export] public int FloatFontSize = 18;

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
        if (DescriptionPanel != null) DescriptionPanel.Hide();
        if (DescriptionLabel != null && UnitData != null)
            DescriptionLabel.Text = $"{UnitData.Description}\nHP:{UnitData.HealthPoints} ATK:{UnitData.AttackPower} AD:{UnitData.AttackDistance} AP:{UnitData.ActionPoints}";

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

    public override void _Process(double delta)
    {
        // 死亡动画：检测到死亡后播放一次，之后不再处理
        if (!_isDying && Unit != null && (Unit.IsDead || !Unit.IsAlive))
        {
            _isDying = true;
            PlayDeathAnimation();
            return;
        }
        // 死亡动画播放中，什么都不做等动画结束
        if (_isDying) return;

        // 正常存活时的安全检测
        if (Unit == null) { QueueFree(); }
    }

    /// <summary>响应 ActionQueue：自己是攻击者时闪白，自己被施加/移除 Buff 时弹跳</summary>
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

            case ApplyBuffAction or RemoveBuffAction:
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
    /// Unit 数据变化时调用：更新标签 + 检测 HP/位置变化触发动画
    /// </summary>
    public void UpdateView()
    {
        if (UnitData == null) return;

        // 标签更新
        if (NameLabel != null) NameLabel.Text = UnitData.UnitName;
        if (HPLabel != null) HPLabel.Text = $" {Unit.CurrentHP}/{Unit.MaxHP}";
        if (ATKLabel != null) ATKLabel.Text = $" {Unit.AttackPower}";

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

        if (ShowDamageNumbers && amount > 0)
            ShowFloatingNumber($"-{amount}", DamageColor);
    }

    private void PlayHealFlash(int amount)
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", HealColor, HealFlashDuration);
        tween.TweenProperty(this, "modulate", Colors.White, HealFlashDuration * 0.5f);

        if (ShowHealNumbers && amount > 0)
            ShowFloatingNumber($"+{amount}", HealColor);
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

    // ========================================================================
    // 浮动数字
    // ========================================================================

    private void ShowFloatingNumber(string text, Color color)
    {
        if (FloatingNumberPrefab == null) return;

        var node = FloatingNumberPrefab.Instantiate<FloatingNumber>();
        node.Position = new Vector2(0, -20);
        AddChild(node);
        node.Show(text, color, FloatLifetime, FloatRise);
    }

    // ========================================================================
    // 鼠标悬停
    // ========================================================================

    public void OnMouseEntered()
    {
        if (DescriptionPanel != null) DescriptionPanel.Show();
    }

    public void OnMouseExited()
    {
        if (DescriptionPanel != null) DescriptionPanel.Hide();
    }
}
