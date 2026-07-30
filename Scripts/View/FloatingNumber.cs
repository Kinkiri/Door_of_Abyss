using Godot;

/// <summary>
/// 浮动伤害/治疗数字预制体脚本。
/// 由 UnitView 实例化，设置数字文本和颜色后自动上飘淡出并销毁。
/// </summary>
public partial class FloatingNumber : Control
{
    [Export] public Label NumberLabel { get; set; }

    /// <summary>显示数字并开始动画</summary>
    /// <param name="text">显示的文本（如 "-3", "+5"）</param>
    /// <param name="color">文本颜色</param>
    /// <param name="duration">动画持续秒数</param>
    /// <param name="rise">上飘像素距离</param>
    public void Show(string text, Color color, float duration, float rise)
    {
        NumberLabel.Text = text;
        NumberLabel.Modulate = color;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "position", Position + Vector2.Up * rise, duration);
        tween.TweenProperty(NumberLabel, "modulate:a", 0, duration * 0.8f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
