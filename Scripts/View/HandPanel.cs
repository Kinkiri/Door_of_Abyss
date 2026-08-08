using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 手牌 UI 面板，监听 CardManager 事件增量同步 CardView。
/// 卡牌动画：抽牌从屏幕右上侧飞入手牌区、用牌向下飞出屏幕销毁、排布平滑调整。
/// </summary>
public partial class HandPanel : Container
{
    [Export] public PackedScene CardViewPrefab;

    /// <summary>鼠标悬停时卡牌的缩放倍率</summary>
    [Export] public float HoverScale { get; set; } = 1.15f;
    /// <summary>缩放渐变动画时长（秒）</summary>
    [Export] public float HoverDuration { get; set; } = 0.3f;
    /// <summary>卡牌之间的间距</summary>
    [Export] public float CardSpacing { get; set; } = 5f;

    /// <summary>排布/飞入的移动平滑时长（秒）</summary>
    [Export] public float LayoutMoveDuration { get; set; } = 0.3f;
    /// <summary>出牌/弃牌向下飞出的动画时长（秒）</summary>
    [Export] public float DrawOutDuration { get; set; } = 0.3f;
    /// <summary>抽牌飞入起点（相对手牌区：X 正=右边缘外、Y 负=上方中部），弧线飞入</summary>
    [Export] public Vector2 DrawInStartOffset { get; set; } = new Vector2(150, -300);
    /// <summary>出牌/弃牌向下飞出屏幕的距离（像素）</summary>
    [Export] public float DrawOutDistance { get; set; } = 600f;

    /// <summary>手牌 Card → CardView 映射（增量同步用）</summary>
    private readonly Dictionary<Card, CardView> _cardViews = new();

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
            LayoutCards();
    }

    /// <summary>横向排列所有参与布局的卡（飞出中的不占槽位），平滑移动到各自位置</summary>
    private void LayoutCards()
    {
        var cards = new List<CardView>();
        foreach (Node child in GetChildren())
            if (child is CardView cv && !cv.IsLeaving)
                cards.Add(cv);
        if (cards.Count == 0) return;

        float totalW = 0;
        foreach (var c in cards)
            totalW += c.Size.X;
        totalW += CardSpacing * (cards.Count - 1);

        float startX = (Size.X - totalW) / 2;
        float y = 0;

        foreach (var c in cards)
        {
            c.SmoothMoveTo(new Vector2(startX, y), LayoutMoveDuration);
            startX += c.Size.X + CardSpacing;
        }
    }

    public override void _Ready()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsUpdated += SyncHand;
    }

    public override void _ExitTree()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsUpdated -= SyncHand;
    }

    /// <summary>
    /// 增量同步手牌（替代全量重建）：
    /// 移除（出牌/弃牌）→ 向下飞出屏幕销毁；新增（抽牌）→ 从右上侧飞入；
    /// 保留的卡由布局平滑补位。
    /// </summary>
    private void SyncHand()
    {
        var cm = CardManager.Instance;
        if (cm == null) return;

        // 1. 移除：不在手牌的卡向下飞出屏幕（不参与后续布局，播完自毁）
        foreach (var kv in _cardViews.ToList())
        {
            if (cm.HandCards.Contains(kv.Key)) continue;
            var view = kv.Value;
            _cardViews.Remove(kv.Key);
            view.IsLeaving = true;
            view.FlyOutAndFree(new Vector2(view.Position.X, view.Position.Y + DrawOutDistance), DrawOutDuration);
        }

        // 2. 新增：实例化并从手牌区右上侧飞入（初始位置设为起点，布局时平滑移到槽位）
        foreach (var card in cm.HandCards)
        {
            if (_cardViews.ContainsKey(card)) continue;
            var view = CardViewPrefab?.Instantiate<CardView>();
            if (view == null) continue;

            view.Card = card;
            view.Pressed += () => SelectionManager.Instance.OnCardClicked(card);
            BindHover(view);
            AddChild(view);
            // 起点 = 屏幕右边缘外侧中部；布局时以弧线飞入槽位（PendingFlyInFrom 只消费一次）
            var flyInFrom = new Vector2(Size.X + DrawInStartOffset.X, DrawInStartOffset.Y);
            view.Position = flyInFrom;
            view.PendingFlyInFrom = flyInFrom;
            _cardViews[card] = view;
        }

        // 3. 按手牌顺序校正树顺序（增量添加不保证插入位置），随后触发平滑布局
        for (int i = 0; i < cm.HandCards.Count; i++)
            if (_cardViews.TryGetValue(cm.HandCards[i], out var cv))
                MoveChild(cv, i);

        QueueSort();
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
