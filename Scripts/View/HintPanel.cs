using Godot;
using System;

/// <summary>
/// 单个提示面板（HintView 的子项，预制体根脚本）。
/// 表现层：负责消息文本、✕ 按钮、自动缩回倒计时与滑入/滑出动画；
/// 排布与销毁节奏由 HintView 统一管理（对齐 CardView/HandPanel 分工模式）。
/// </summary>
public partial class HintPanel : PanelContainer
{
    /// <summary>是否正在退出（滑出动画中）：不参与 HintView 布局，播完自毁</summary>
    public bool IsLeaving { get; set; }

    /// <summary>待滑入起点 X（非 null 时首次 SmoothMoveTo 用水平滑入替代直线，滑完自动清除）</summary>
    public float? PendingSlideInFromX { get; set; }

    /// <summary>请求关闭（✕ 按下 / 倒计时到点）→ HintView 接管销毁与重排</summary>
    public event Action<HintPanel> CloseRequested;

    [Export] public Label MessageLabel;
    [Export] public Button CloseButton;

    private Tween _moveTween;
    private Vector2 _moveTweenTarget;
    private SceneTreeTimer _retractTimer;

    /// <summary>初始化内容并绑定交互。</summary>
    public void Setup(string message, bool autoRetract, float hoverDuration)
    {
        if (MessageLabel != null)
            MessageLabel.Text = message;

        if (CloseButton != null)
            CloseButton.Pressed += OnClosePressed;

        if (autoRetract && hoverDuration > 0f)
        {
            // processAlways=false：暂停（Esc）时倒计时冻结，恢复后继续
            _retractTimer = GetTree().CreateTimer(hoverDuration, processAlways: false);
            _retractTimer.Timeout += OnClosePressed;
        }
    }

    // ======================================================================
    // 位置动画（滑入 / 平滑移动 / 滑出）
    // ======================================================================

    /// <summary>平滑移动到目标位置（Kill 旧动画防连动抖动）。
    /// 若挂有待滑入起点（PendingSlideInFromX），首次调用改为从屏幕右侧滑入（起点 Y 固定顶部）。</summary>
    public void SmoothMoveTo(Vector2 target, float duration)
    {
        // 已在前往同一目标：跳过（防止重复 sort 反复重启动画造成抖动）
        if (_moveTween != null && _moveTween.IsValid() && _moveTween.IsRunning() && _moveTweenTarget == target)
            return;

        _moveTween?.Kill();
        if (Position.DistanceTo(target) < 1f)
        {
            Position = target;
            return;
        }

        if (PendingSlideInFromX.HasValue)
        {
            float fromX = PendingSlideInFromX.Value;
            PendingSlideInFromX = null;
            // 起点 Y 固定容器顶部：与槽位 Y 解耦，避免高度未定时的槽位偏差
            Position = new Vector2(fromX, 0f);
        }

        _moveTweenTarget = target;
        _moveTween = CreateTween();
        // Expo.Out：初始极快冲入，速度指数级衰减后缓缓停稳
        _moveTween.SetTrans(Tween.TransitionType.Expo);
        _moveTween.SetEase(Tween.EaseType.Out);
        _moveTween.TweenProperty(this, "position", target, duration);
    }

    /// <summary>向右滑出屏幕并淡出后自毁（关闭动画）</summary>
    public void PlaySlideOutAndFree(float duration)
    {
        _moveTween?.Kill();
        _moveTween = CreateTween();
        _moveTween.SetTrans(Tween.TransitionType.Expo);
        _moveTween.SetEase(Tween.EaseType.In);
        _moveTween.TweenProperty(this, "position", Position + new Vector2(Size.X + 40f, 0), duration);
        _moveTween.Parallel().TweenProperty(this, "modulate:a", 0f, duration);
        _moveTween.TweenCallback(Callable.From(QueueFree));
    }

    private void OnClosePressed()
    {
        if (IsLeaving) return;   // 已在退出中（倒计时与 ✕ 双触发防重）
        IsLeaving = true;
        StopRetractTimer();
        CloseRequested?.Invoke(this);
    }

    /// <summary>退订自动缩回倒计时（X 关闭/退出树时取消，防止对已释放节点回调）</summary>
    private void StopRetractTimer()
    {
        if (_retractTimer != null)
        {
            _retractTimer.Timeout -= OnClosePressed;
            _retractTimer = null;
        }
    }

    public override void _ExitTree()
    {
        StopRetractTimer();
        if (CloseButton != null)
            CloseButton.Pressed -= OnClosePressed;
    }
}
