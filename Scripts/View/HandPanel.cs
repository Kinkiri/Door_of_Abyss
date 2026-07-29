using Godot;

/// <summary>
/// 手牌 UI 面板，监听 CardManager 事件创建/销毁 CardView，
/// 管理卡牌交互反馈（描画瞄准线、鼠标悬停放大）
/// </summary>
public partial class HandPanel : Container
{
    [Export] public PackedScene CardViewPrefab;

    /// <summary>鼠标悬停时卡牌的缩放倍率</summary>
    [Export] public float HoverScale { get; set; } = 1.15f;
    /// <summary>缩放渐变动画时长（秒）</summary>
    [Export] public float HoverDuration { get; set; } = 0.3f;
    /// <summary>卡牌之间的间距</summary>
    [Export] public float CardSpacing { get; set; } = 20f;

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
            LayoutCards();
    }

    /// <summary>横向排列所有子元素，居中</summary>
    private void LayoutCards()
    {
        var children = GetChildren();
        if (children.Count == 0) return;

        float totalW = 0;
        foreach (Node child in children)
        {
            if (child is Control c)
                totalW += c.Size.X;
        }
        totalW += CardSpacing * (children.Count - 1);

        float startX = (Size.X - totalW) / 2;
        float y = 0;

        foreach (Node child in children)
        {
            if (child is Control c)
            {
                FitChildInRect(c, new Rect2(startX, y, c.Size.X, Size.Y));
                startX += c.Size.X + CardSpacing;
            }
        }
    }

    public override void _Ready()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsUpdated += RebuildHand;
    }

    public override void _ExitTree()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsUpdated -= RebuildHand;
    }

    // ======================================================================
    // 手牌重建
    // ======================================================================

    private void RebuildHand()
    {
        foreach (Node child in GetChildren())
            child.QueueFree();

        foreach (var card in CardManager.Instance.HandCards)
        {
            var view = CardViewPrefab?.Instantiate<CardView>();
            if (view == null) continue;

            view.Card = card;
            view.Pressed += () => SelectionManager.Instance.OnCardClicked(card);
            BindHover(view);
            AddChild(view);
        }
    }

    /// <summary>绑定悬停放大效果</summary>
    private void BindHover(CardView view)
    {
        Tween activeTween = null;

        view.MouseEntered += () =>
        {
            activeTween?.Kill();
            activeTween = CreateTween();
            activeTween.TweenProperty(view, "scale", Vector2.One * HoverScale, HoverDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        };

        view.MouseExited += () =>
        {
            activeTween?.Kill();
            activeTween = CreateTween();
            activeTween.TweenProperty(view, "scale", Vector2.One, HoverDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        };
    }

    // ======================================================================
    // 瞄准线
    // ======================================================================

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        // 为每张被选中的卡牌画瞄准线
        foreach (Node child in GetChildren())
        {
            if (child is CardView cv && SelectionManager.Instance?.SelectedCard == cv.Card)
            {
                var from = cv.Position + cv.Size / 2;
                var to = GetGlobalMousePosition() - GlobalPosition;
                DrawLine(from, to, Colors.White, 2);
            }
        }
    }
}
