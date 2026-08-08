using Godot;

/// <summary>
/// 卡牌视觉实体，展示卡牌信息，悬停效果由 HandPanel 统一控制
/// </summary>
public partial class CardView : TextureButton
{
    public Card Card { get; set; }

    /// <summary>是否正在飞出（出牌/弃牌动画中）：不参与手牌布局，播完自动销毁</summary>
    public bool IsLeaving { get; set; }

    /// <summary>待飞入起点（非 null 时首次布局用弧线飞入替代直线平滑移动，飞完自动清除）</summary>
    public Vector2? PendingFlyInFrom { get; set; }

    /// <summary>抽牌飞入弧线的拱高（控制点抬升高度，越大弧线越弯）</summary>
    [Export] public float FlyInArcHeight { get; set; } = 120f;

    /// <summary>抽牌弧线飞入的时长（秒），独立于排布平滑时长，便于放慢突出弧线</summary>
    [Export] public float FlyInDuration { get; set; } = 0.6f;

    private Tween _moveTween;

    /// <summary>弧线飞入中：布局每帧会调用 SmoothMoveTo，飞入期间跳过（否则下一帧直线分支会 Kill 弧线 tween）</summary>
    private bool _isFlyingIn;

    [Export] public Label CardName;
    [Export] public Label CardCost;
    [Export] public Label CardDescription;
    [Export] public Panel DescriptionPanel;

    public override void _Ready()
    {
        if (Card == null)
        {
            GD.PrintErr("CardView: Card 未赋值");
            return;
        }

        if (DescriptionPanel != null)
            DescriptionPanel.Visible = false;

        MouseEntered += () => ShowDescription(true);
        MouseExited += () => ShowDescription(false);

        UpdateView();
    }

    public void UpdateView()
    {
        if (Card == null) return;

        if (CardName != null)
            CardName.Text = Card.CardName;
        if (CardCost != null)
            CardCost.Text = $"{Card.Cost}";
        if (CardDescription != null)
            CardDescription.Text = Card.Description;
        if (Card.CardData is UnitCardData unitCard)
        {
            if (unitCard.UnitData != null)
            {
                CardDescription.Text += $"\n" +
                                        $"{unitCard.UnitData.Description} " +
                                        $"HP: {unitCard.UnitData.HealthPoints} " +
                                        $"ATK: {unitCard.UnitData.AttackPower}\n" +
                                        $"AD: {CellShape.DescribeRange(unitCard.UnitData.AttackShape, unitCard.UnitData.AttackDistance)} " +
                                        $"AP: {unitCard.UnitData.ActionPoints}";
            }
        }
        if (Card.CardData is EquipmentCardData equipmentCard)
        {
            if (equipmentCard.EquipmentData != null)
            {
                CardDescription.Text += $"\n" +
                                        $"{equipmentCard.EquipmentData.Description}" +
                                        $"\n" +
                                        $"AB: {equipmentCard.EquipmentData.AttackBonus} " +
                                        $"MHB: {equipmentCard.EquipmentData.MaxHealthBonus}";
            }
        }
    }

    private void ShowDescription(bool visible)
    {
        if (DescriptionPanel == null) return;
        DescriptionPanel.Visible = visible;
    }

    // ========================================================================
    // 位置动画（抽牌飞入 / 出牌飞出 / 排布平滑共用）
    // ========================================================================

    /// <summary>平滑移动到目标位置（Kill 旧移动动画防连抽/连出抖动）。
    /// 若挂有待飞入起点（PendingFlyInFrom），首次布局改用弧线飞入替代直线。</summary>
    public void SmoothMoveTo(Vector2 target, float duration)
    {
        if (_isFlyingIn) return;   // 弧线飞入中，布局不接管（避免 Kill 弧线 tween 改直线）
        _moveTween?.Kill();
        if (Position.DistanceTo(target) < 1f)
        {
            Position = target;
            return;
        }

        if (PendingFlyInFrom.HasValue)
        {
            Vector2 from = PendingFlyInFrom.Value;
            PendingFlyInFrom = null;
            FlyInArc(from, target, FlyInArcHeight, FlyInDuration);
            return;
        }

        _moveTween = CreateTween();
        _moveTween.SetTrans(Tween.TransitionType.Quad);
        _moveTween.SetEase(Tween.EaseType.Out);
        _moveTween.TweenProperty(this, "position", target, duration);
    }

    /// <summary>沿二次贝塞尔弧线从起点飞入终点（抽牌飞入：控制点在两端上方，弧线先扬后落）。
    /// 控制点钳制在视口顶部内——起点较高时弧高会把弧顶顶出屏幕（曲线不可见段），钳制后曲线全程可见。</summary>
    private void FlyInArc(Vector2 from, Vector2 to, float arcHeight, float duration)
    {
        _isFlyingIn = true;
        Position = from;
        var control = new Vector2((from.X + to.X) / 2f, Mathf.Min(from.Y, to.Y) - arcHeight);
        // 视口顶在 CardView 局部坐标；换算回"相对父节点（手牌区）"的控制点下限
        float viewportTopCardLocal = GetViewportRect().Position.Y;
        float minControlY = viewportTopCardLocal + from.Y + 20f;
        control.Y = Mathf.Max(control.Y, minControlY);
        _moveTween = CreateTween();
        _moveTween.SetTrans(Tween.TransitionType.Quad);
        _moveTween.SetEase(Tween.EaseType.Out);
        _moveTween.TweenMethod(Callable.From((float t) =>
        {
            float inv = 1f - t;
            Position = inv * inv * from + 2f * inv * t * control + t * t * to;
        }), 0f, 1f, duration);
        _moveTween.Finished += () => _isFlyingIn = false;
    }

    /// <summary>向下飞出屏幕后销毁（出牌/弃牌动画）</summary>
    public void FlyOutAndFree(Vector2 target, float duration)
    {
        _moveTween?.Kill();
        _moveTween = CreateTween();
        _moveTween.SetTrans(Tween.TransitionType.Quad);
        _moveTween.SetEase(Tween.EaseType.In);
        _moveTween.TweenProperty(this, "position", target, duration);
        _moveTween.TweenCallback(Callable.From(QueueFree));
    }
}
