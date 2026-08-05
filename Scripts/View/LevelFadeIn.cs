using Godot;

/// <summary>
/// 战斗场景渐变入场：全屏黑幕加载时覆盖，入场后淡出并放行输入。
/// </summary>
public partial class LevelFadeIn : CanvasLayer
{
    [Export] private ColorRect _black;

    public override void _Ready()
    {
        _black.Color = new Color(0, 0, 0, 1);
        _black.MouseFilter = Control.MouseFilterEnum.Stop;
        Tween tween = CreateTween();
        tween.TweenInterval(1.0f);   // 全黑停留 1 秒，再开始渐出
        tween.TweenProperty(_black, "color:a", 0f, 0.9f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
            _black.MouseFilter = Control.MouseFilterEnum.Ignore));
    }
}
