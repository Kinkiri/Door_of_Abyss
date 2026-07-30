using Godot;

/// <summary>
/// 浮动伤害/治疗数字。由 UnitView 实例化，设置文本后自动上飘淡出并销毁。
/// 继承 Node2D 而非 Control，避免布局系统干扰定位。
/// </summary>
public partial class FloatingNumber : Node2D
{
    private Label _label;

    public override void _Ready()
    {
        // _Ready 时创建 Label
        _label = new Label();
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.SetSize(new Vector2(60, 30));
        _label.Position = new Vector2(-30, -15);
        _label.AddThemeFontSizeOverride("font_size", 18);
        AddChild(_label);
    }

    /// <summary>显示数字并开始动画</summary>
    public void Show(string text, Color color, float duration, float rise)
    {
        if (_label == null) _label = GetNode<Label>("Label");
        _label.Text = text;
        _label.Modulate = color;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "position", Position + Vector2.Up * rise, duration);
        tween.TweenProperty(_label, "modulate:a", 0, duration * 0.8f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
