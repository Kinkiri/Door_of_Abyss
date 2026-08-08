using Godot;
using System.Collections.Generic;

/// <summary>
/// 对局内提示总容器（Container 子类，仿 HandPanel 自定义排布模式）：
/// 统一管理所有提示面板的创建、排布与销毁。
/// 订阅 BattleManager.PhaseChanged，按 LevelData.Hints 的触发回合自动显示；
/// 新提示插入顶部，旧提示平滑下移补位；关闭的提示不占槽位，其余平滑上移。
/// </summary>
public partial class HintView : Container
{
    [Export] public PackedScene HintPanelPrefab;

    /// <summary>提示面板固定宽度</summary>
    [Export] public float ItemWidth { get; set; } = 490f;

    /// <summary>提示之间的间距</summary>
    [Export] public float Spacing { get; set; } = 8f;

    /// <summary>排布/顶动补位的移动平滑时长（秒）</summary>
    [Export] public float LayoutMoveDuration { get; set; } = 0.3f;

    /// <summary>新提示从右侧滑入的时长（秒）</summary>
    [Export] public float SlideInDuration { get; set; } = 0.35f;

    /// <summary>关闭时向右滑出的时长（秒）</summary>
    [Export] public float SlideOutDuration { get; set; } = 0.3f;

    /// <summary>已触发过的提示（同一提示只显示一次）</summary>
    private readonly HashSet<HintData> _fired = new();

    public override void _Ready()
    {
        var bm = BattleManager.Instance;
        if (bm != null)
            bm.PhaseChanged += OnPhaseChanged;
    }

    public override void _ExitTree()
    {
        var bm = BattleManager.Instance;
        if (bm != null)
            bm.PhaseChanged -= OnPhaseChanged;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
            LayoutHints();
    }

    /// <summary>回合阶段切换：GameStart（第 0 回合）与 RoundStart（第 N 回合）触发对应提示</summary>
    private void OnPhaseChanged(BattlePhase phase, Team team, int round)
    {
        switch (phase)
        {
            case BattlePhase.GameStart:  FireHints(0);     break;   // 放门/游戏开始阶段
            case BattlePhase.RoundStart: FireHints(round); break;
        }
    }

    private void FireHints(int round)
    {
        var hints = BattleManager.Instance?.LevelData?.Hints;
        if (hints == null) return;
        foreach (var h in hints)
        {
            if (h == null || h.TriggerRound != round || !_fired.Add(h)) continue;
            ShowHint(h);
        }
    }

    /// <summary>创建一条提示（程序化调用也可用：任意时刻显示一条临时提示）</summary>
    public void ShowHint(HintData data)
    {
        if (data == null || HintPanelPrefab == null) return;

        var panel = HintPanelPrefab.Instantiate<HintPanel>();
        if (panel == null) return;

        AddChild(panel);          // 先入树：Setup 中的 GetTree()（自动缩回倒计时）依赖节点在树内
        MoveChild(panel, 0);      // 置顶：新提示插入最上方，旧提示被顶下
        panel.Setup(data.Message, data.AutoRetract, data.HoverDuration);
        panel.CloseRequested += OnPanelCloseRequested;
        // 起点 = 容器右边缘外侧（屏幕右侧外），布局时平滑滑入
        panel.PendingSlideInFromX = Size.X + ItemWidth + 20f;
        QueueSort();
    }

    /// <summary>提示请求关闭：滑出动画自毁，其余提示平滑上移补位</summary>
    private void OnPanelCloseRequested(HintPanel panel)
    {
        if (panel == null) return;
        panel.PlaySlideOutAndFree(SlideOutDuration);
        QueueSort();
    }

    /// <summary>纵向右对齐排布所有存活提示（仿 HandPanel.LayoutCards）：固定宽度、
    /// 内容高度自适应。新面板刚加入时内部 Label 尚未按宽度换行（min height 为
    /// 宽度=0 时的巨大值），先隐藏并设尺寸，等高度稳定后（引擎再触发 sort）再显示滑入——
    /// 避免"巨大高度的面板闪一帧"或把后续提示顶出屏幕。</summary>
    private void LayoutHints()
    {
        var hints = new List<HintPanel>();
        foreach (Node child in GetChildren())
            if (child is HintPanel hp && !hp.IsLeaving)
                hints.Add(hp);
        if (hints.Count == 0) return;

        bool unstable = false;
        foreach (var hp in hints)
        {
            float minH = hp.GetMinimumSize().Y;
            if (Mathf.Abs(minH - hp.Size.Y) > 1f)
            {
                // 高度仍在收敛（Label 换行后 min height 变化）：应用新尺寸、暂隐藏
                hp.Size = new Vector2(ItemWidth, minH);
                hp.Visible = false;
                unstable = true;
            }
            else
            {
                hp.Size = new Vector2(ItemWidth, minH);
                if (!hp.Visible)
                    hp.Visible = true;   // 高度就绪：显示，随后启动滑入/补位动画
            }
        }
        if (unstable) return;   // 等 min size 稳定（引擎会再次触发 sort）

        float y = 0f;
        foreach (var hp in hints)
        {
            var target = new Vector2(Size.X - ItemWidth, y);   // 右对齐
            // 新提示（待滑入）用滑入时长，旧提示顶动补位用排布时长
            float duration = hp.PendingSlideInFromX.HasValue ? SlideInDuration : LayoutMoveDuration;
            hp.SmoothMoveTo(target, duration);
            y += hp.Size.Y + Spacing;
        }
    }
}
