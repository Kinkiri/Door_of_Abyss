using Godot;

/// <summary>
/// 浮动数字（世界空间 Node2D）：受伤/治疗数字的独立实例。
/// 从单位身上以随机方向/初速度"爆出"，受重力形成抛物线；字号随数值（伤害/治疗量）增大；
/// 淡出后自毁。_Process 受暂停影响 → 暂停时数字停留，恢复后继续（行为同旧方案）。
/// </summary>
public partial class FloatingNumber : Node2D
{
    [Export] public Label Label { get; set; }

    // ── 爆出动画参数（预制体 Inspector 可调）──────────────────────────────
    [ExportGroup("爆出")]
    [Export] public float Lifetime = 1f;
    [Export] public float AnchorOffsetY = -30f;    // 起点基准：单位头顶偏移（负=向上）
    [Export] public float ScatterRadius = 20f;     // 起始位置在单位周围随机散布范围（不随段数累积）
    [Export] public float MinSpeed = 80f;          // 初速度随机区间（世界 px/s）
    [Export] public float MaxSpeed = 160f;
    [Export] public float MinAngleDeg = 60f;       // 发射角随机区间（0=正右，90=正上）
    [Export] public float MaxAngleDeg = 120f;
    [Export] public float Gravity = 220f;          // 重力加速度（px/s²，向下）

    // ── 字号参数 ──────────────────────────────────────────────────────────
    [ExportGroup("字号")]
    [Export] public float BaseFontSize = 40f;
    [Export] public float FontSizePerAmount = 2.5f; // 每点数值的字号增量
    [Export] public float MaxFontSize = 90f;

    private Vector2 _position;
    private Vector2 _velocity;
    private float _elapsed;

    /// <summary>
    /// 初始化：锚点/文本/颜色/数值（数值决定字号）。
    /// 起始点 = 单位头顶基准 + 周围小范围随机（多段数字各自散开，不会随段数越飘越高）；
    /// 之后**独立飞行**（不再跟随锚点——爆出后数字飞走，单位移动/销毁不影响轨迹）。
    /// </summary>
    public void Setup(UnitView anchor, string text, Color color, int amount)
    {
        var scatter = new Vector2(
            (float)GD.RandRange(-ScatterRadius, ScatterRadius),
            (float)GD.RandRange(-ScatterRadius, ScatterRadius));
        _position = anchor.GlobalPosition + new Vector2(0, AnchorOffsetY) + scatter;

        float speed = (float)GD.RandRange(MinSpeed, MaxSpeed);
        float angle = Mathf.DegToRad((float)GD.RandRange(MinAngleDeg, MaxAngleDeg));
        _velocity = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * speed;

        _elapsed = 0;
        GlobalPosition = _position;

        if (Label != null)
        {
            Label.Text = text;
            Label.Modulate = color;
            float size = Mathf.Min(BaseFontSize + Mathf.Max(0, amount) * FontSizePerAmount, MaxFontSize);
            Label.AddThemeFontSizeOverride("font_size", (int)size);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        // 半隐式欧拉：位置含重力项，速度再累加重力
        _position += _velocity * dt + new Vector2(0, 0.5f * Gravity * dt * dt);
        _velocity += new Vector2(0, Gravity * dt);
        _elapsed += dt;

        // 后 40% 时长淡出（根节点 modulate 乘到子 Label，只改 alpha 不干扰文本颜色）
        float fadeStart = Lifetime * 0.6f;
        if (_elapsed > fadeStart)
        {
            float t = Mathf.Clamp((_elapsed - fadeStart) / (Lifetime * 0.4f), 0f, 1f);
            Modulate = new Color(1, 1, 1, 1f - t);
        }

        GlobalPosition = _position;

        if (_elapsed >= Lifetime)
        {
            QueueFree();
        }
    }
}
