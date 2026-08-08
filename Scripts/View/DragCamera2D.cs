using Godot;

namespace DoorofAbbyss.UI;

[GlobalClass]
public partial class DragCamera2D : Camera2D
{
    public static DragCamera2D Instance { get; private set; }

    /// <summary>
    /// 拖拽灵敏度，控制鼠标拖动时摄像机移动的速度。
    /// </summary>
    [Export] public float DragSensitivity { get; set; } = 1.0f;

    /// <summary>
    /// 缩放步长（线性步进），控制鼠标滚轮缩放时摄像机缩放的增量。
    /// </summary>
    [Export] public float ZoomStep { get; set; } = 0.1f;

    /// <summary>
    /// 最小缩放值，限制摄像机缩放的下限。
    /// </summary>
    [Export] public float MinZoom { get; set; } = 0.1f;

    /// <summary>
    /// 最大缩放值，限制摄像机缩放的上限。
    /// </summary>
    [Export] public float MaxZoom { get; set; } = 10.0f;

    /// <summary>
    /// 是否启用拖拽和缩放功能，默认启用。
    /// </summary>
    [Export] public bool IsEnabledAction { get; set; } = true;

    /// <summary>
    /// 滚轮缩放的平滑动画时长（秒）。值越小越跟手，越大越绵软。
    /// </summary>
    [Export] public float ZoomAnimationDuration { get; set; } = 0.12f;

    /// <summary>是否启用摄像机跟随（选中聚焦 + 行动跟随）</summary>
    [Export] public bool EnableFollow { get; set; } = true;

    /// <summary>跟随平滑速度（指数平滑系数，越大追得越快）</summary>
    [Export] public float FollowSpeed { get; set; } = 6f;

    /// <summary>选中单位时聚焦该单位</summary>
    [Export] public bool FollowOnSelect { get; set; } = true;

    /// <summary>单位（玩家/AI）移动或攻击时跟随行动单位</summary>
    [Export] public bool FollowOnAct { get; set; } = true;

    /// <summary>双点跟随（攻击者+目标）时，两者距视口边缘保留的世界边距（像素），用于自适应缩放计算</summary>
    [Export] public float FollowPadding { get; set; } = 200f;

    private bool _isDragging;
    private Vector2 _dragStartScreenPos;
    private Vector2 _dragStartCameraPos;

    /// <summary>缩放目标（线性步进累计，X=Y）</summary>
    private float _targetZoom = 1f;

    /// <summary>进行中的缩放动画（快速连滚时 Kill 旧动画从当前值合并，不跳变）</summary>
    private Tween _zoomTween;

    /// <summary>缩放动画锚点：动画期间保持该世界点不动（鼠标跟随缩放）</summary>
    private Vector2 _zoomAnchorWorld;
    private Vector2 _zoomAnchorScreen;

    /// <summary>当前跟随的单位视图（null = 不跟随）</summary>
    private Node2D _followTarget;
    private Unit _followUnit;

    /// <summary>次关注单位视图（攻击目标，null = 单点跟随）</summary>
    private Node2D _followTarget2;
    private Unit _followUnit2;

    /// <summary>行动前预告的镜头目标点（优先级最高；行动后由 UnitActed 建立的真实跟随接管时清除）</summary>
    private Vector2? _previewPoint;

    /// <summary>自动缩放目标（双点/预告跟随按距离计算，_Process 指数平滑，与位置跟随并行；null=不缩放）。
    /// 手动滚轮缩放时清除（用户接管）。</summary>
    private float? _autoTargetZoom;

    public override void _Ready()
    {
        Instance = this;
        _targetZoom = Zoom.X;

        // Manager 节点在场景树中先于本节点 _Ready，延迟到帧尾再订阅，避免 Instance 为 null
        CallDeferred(nameof(SubscribeFollow));
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        UnsubscribeFollow();
    }

    private void SubscribeFollow()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionUpdated += OnSelectionUpdated;
        if (BattleManager.Instance != null)
            BattleManager.Instance.UnitActed += OnUnitActed;
        if (UnitManager.Instance != null)
            UnitManager.Instance.OnUnitRemoved += OnUnitRemoved;
        if (EnemyAI.Instance != null)
        {
            EnemyAI.Instance.AiAttackPreviewed += OnAiAttackPreviewed;
            EnemyAI.Instance.AiMovePreviewed += OnAiMovePreviewed;
        }
    }

    private void UnsubscribeFollow()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionUpdated -= OnSelectionUpdated;
        if (BattleManager.Instance != null)
            BattleManager.Instance.UnitActed -= OnUnitActed;
        if (UnitManager.Instance != null)
            UnitManager.Instance.OnUnitRemoved -= OnUnitRemoved;
        if (EnemyAI.Instance != null)
        {
            EnemyAI.Instance.AiAttackPreviewed -= OnAiAttackPreviewed;
            EnemyAI.Instance.AiMovePreviewed -= OnAiMovePreviewed;
        }
    }

    /// <summary>AI 行动预告（敌人行动前 0.4s 发出）：镜头先飞向行动位置</summary>
    private void OnAiAttackPreviewed(Unit enemy, Unit target) => PreviewFollow(enemy, target);
    private void OnAiMovePreviewed(Unit enemy, Vector2I grid) => PreviewFollow(enemy, grid);

    // ======================================================================
    // 跟随（非线性丝滑：每帧指数平滑逼近目标，帧率无关、无过冲）
    // ======================================================================

    /// <summary>上次跟随的选中单位（用于识别"选中变化" vs "同一单位状态刷新"）</summary>
    private Unit _lastSelectedUnit;

    /// <summary>选中状态变化：选中单位 → 聚焦；取消选中 → 停止跟随。
    /// 仅当选中的单位实例变化时响应——单位状态更新（范围重算等）同样发 SelectionUpdated，
    /// 若每次都重新 SetFollowTarget 会把行动跟随（如攻击后的双点跟随）覆盖回单点。</summary>
    private void OnSelectionUpdated()
    {
        if (!EnableFollow || !FollowOnSelect) return;

        var unit = SelectionManager.Instance?.SelectedUnit;
        if (unit == _lastSelectedUnit)
        {
            GD.Print($"[Camera] SelectionUpdated 忽略（选中未变）: {unit?.UnitData?.UnitName}");
            return;
        }
        _lastSelectedUnit = unit;
        GD.Print($"[Camera] SelectionUpdated 选中变化: {unit?.UnitData?.UnitName}");

        if (unit != null)
            SetFollowTarget(unit);
        else
            ClearFollow();
    }

    /// <summary>任意单位（玩家/AI）完成移动或攻击：跟随行动单位与目标（若有）的中点（优先级高于选中）。
    /// 攻击时目标为受击单位 → 双点跟随；移动时目标为 null → 单点跟随。</summary>
    private void OnUnitActed(Unit unit, Unit target)
    {
        if (!EnableFollow || !FollowOnAct) return;
        GD.Print($"[Camera] UnitActed: 行动={unit?.UnitData?.UnitName} 目标={target?.UnitData?.UnitName}");
        if (unit != null)
            SetFollowTarget(unit, target);
    }

    /// <summary>跟随单位被移除：主关注单位移除 → 停止跟随；次关注单位移除 → 退化为单点跟随</summary>
    private void OnUnitRemoved(Unit unit)
    {
        if (unit == _followUnit)
            ClearFollow();
        else if (unit == _followUnit2)
        {
            GD.Print($"[Camera] 次关注单位移除，退化单点: {unit?.UnitData?.UnitName}");
            _followUnit2 = null;
            _followTarget2 = null;
            _autoTargetZoom = null;   // 停止继续缩小（保持当前缩放，用户可手动放大）
        }
    }

    /// <summary>敌人行动前预告镜头：先飞向攻击双方（单位+目标单位）中点并自适应缩放，
    /// 停顿（CameraPanDelay）后再执行行动；行动后 UnitActed 建立真实跟随接管。</summary>
    public void PreviewFollow(Unit unit, Unit target)
    {
        if (!EnableFollow) return;
        var um = UnitViewManager.Instance;
        var va = um?.GetUnitView(unit);
        var vb = um?.GetUnitView(target);
        if (va == null || vb == null) return;
        Vector2 a = va.GlobalPosition, b = vb.GlobalPosition;
        _previewPoint = (a + b) / 2f;
        ApplyAutoZoom(a, b);
        GD.Print($"[Camera] 预告(攻击): {unit?.UnitData?.UnitName} + {target?.UnitData?.UnitName}");
    }

    /// <summary>敌人行动前预告镜头：先飞向移动单位与目标格中点并自适应缩放（移动预告）</summary>
    public void PreviewFollow(Unit unit, Vector2I targetGrid)
    {
        if (!EnableFollow) return;
        var va = UnitViewManager.Instance?.GetUnitView(unit);
        if (va == null) return;
        Vector2 a = va.GlobalPosition;
        Vector2 b = MapManager.Instance?.GridToWorld(targetGrid) ?? a;
        _previewPoint = (a + b) / 2f;
        ApplyAutoZoom(a, b);
        GD.Print($"[Camera] 预告(移动): {unit?.UnitData?.UnitName} → {targetGrid}");
    }

    private void SetFollowTarget(Unit unit)
    {
        SetFollowTarget(unit, null);
    }

    private void SetFollowTarget(Unit unit, Unit secondary)
    {
        var um = UnitViewManager.Instance;
        _followUnit = unit;
        _followTarget = um?.GetUnitView(unit);   // 无视图（测试环境等）时为 null，不跟随
        _followUnit2 = secondary;
        _followTarget2 = secondary != null ? um?.GetUnitView(secondary) : null;
        _previewPoint = null;   // 真实跟随接管，清除行动前预告

        GD.Print($"[Camera] SetFollowTarget: {(secondary != null ? $"双点 {unit?.UnitData?.UnitName} + {secondary?.UnitData?.UnitName}" : $"单点 {unit?.UnitData?.UnitName}")}" +
                 $" 视图2={(_followTarget2 != null ? "有" : "无")}");

        // 双点跟随建立时按两单位距离自适应缩小（只缩小不放大），保证两者同屏可见；
        // 仅执行一次，之后用户滚轮手动缩放优先，直到下次跟随建立再重新评估；单点跟随无缩放约束
        if (_followTarget2 != null)
            ApplyAutoZoom(_followTarget.GlobalPosition, _followTarget2.GlobalPosition);
        else
            _autoTargetZoom = null;
    }

    /// <summary>双点/预告跟随的自适应缩放：目标缩放 = 视口短边 / (两单位距离 + 边距)。
    /// 只缩小不放大；缩放由 _Process 指数平滑执行，与位置跟随并行。</summary>
    private void ApplyAutoZoom(Vector2 a, Vector2 b)
    {
        float dist = a.DistanceTo(b);
        float shortSide = Mathf.Min(GetViewportRect().Size.X, GetViewportRect().Size.Y);
        float required = shortSide / Mathf.Max(dist + FollowPadding, 1f);
        if (required < Zoom.X)
            _autoTargetZoom = required;
    }

    private void ClearFollow()
    {
        _followUnit = null;
        _followTarget = null;
        _followUnit2 = null;
        _followTarget2 = null;
        _lastSelectedUnit = null;
        _previewPoint = null;
        _autoTargetZoom = null;
    }

    public override void _Process(double delta)
    {
        if (!EnableFollow) return;

        // 用户手动操作（拖拽/缩放动画中）暂停跟随，避免抢位置
        if (_isDragging) return;
        if (_zoomTween != null && _zoomTween.IsValid()) return;

        if (_followTarget == null || !GodotObject.IsInstanceValid(_followTarget))
        {
            // 视图已销毁（单位死亡等）→ 停止跟随
            ClearFollow();
            return;
        }

        // 跟随点优先级：行动前预告点 > 双点（攻击者+目标）中点 > 单点；次视图已销毁（目标死亡）→ 退化为单点
        Vector2 targetPos;
        if (_previewPoint.HasValue)
        {
            targetPos = _previewPoint.Value;
        }
        else if (_followTarget2 != null && GodotObject.IsInstanceValid(_followTarget2))
        {
            targetPos = (_followTarget.GlobalPosition + _followTarget2.GlobalPosition) / 2f;
        }
        else
        {
            targetPos = _followTarget.GlobalPosition;
        }

        // 非线性指数平滑：起始快、接近慢（位置与自动缩放共用同一系数，同步并行过渡）
        float t = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(targetPos, t);

        // 自动缩放与位置跟随并行（不走 tween——tween 期间位置会被暂停，形成串行"先缩后移"）
        if (_autoTargetZoom.HasValue)
        {
            float az = _autoTargetZoom.Value;
            float newZoom = Zoom.X + (az - Zoom.X) * t;
            if (Mathf.Abs(newZoom - az) <= 0.002f)
            {
                newZoom = az;
                _autoTargetZoom = null;
            }
            Zoom = new Vector2(newZoom, newZoom);
        }
    }

    // ======================================================================
    // 输入
    // ======================================================================

    /// <summary>
    /// 处理输入事件，包括鼠标中键拖动和滚轮缩放。
    /// </summary>
    /// <param name="event"></param>
    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mouseButton:
                HandleMouseButton(mouseButton);
                break;

            case InputEventMouseMotion mouseMotion when _isDragging:
                HandleMouseDrag(mouseMotion);
                break;
        }
    }

    /// <summary>
    /// 处理鼠标按钮事件，包括中键拖动和滚轮缩放。
    /// </summary>
    /// <param name="mouseButton"></param>
    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        switch (mouseButton.ButtonIndex)
        {
            case MouseButton.Middle:
                if (mouseButton.Pressed)
                {
                    // 拖拽前打断缩放动画并取消跟随（用户手动接管，不再吸回）
                    _zoomTween?.Kill();
                    _targetZoom = Zoom.X;
                    ClearFollow();
                    _isDragging = true;
                    _dragStartScreenPos = mouseButton.Position;
                    _dragStartCameraPos = GlobalPosition;
                }
                else
                {
                    _isDragging = false;
                }
                GetViewport().SetInputAsHandled();
                break;

            case MouseButton.WheelUp:
                SetTargetZoom(_targetZoom + ZoomStep);
                GetViewport().SetInputAsHandled();
                break;

            case MouseButton.WheelDown:
                SetTargetZoom(_targetZoom - ZoomStep);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    /// <summary>
    /// 设置缩放目标并启动平滑动画。以鼠标为锚点：动画期间保持鼠标下的世界点不动
    /// （zoom-to-cursor），放大跟随鼠标位置。线性步进，Clamp 到 [MinZoom, MaxZoom]。
    /// </summary>
    private void SetTargetZoom(float target)
    {
        target = Mathf.Clamp(target, MinZoom, MaxZoom);
        if (Mathf.IsEqualApprox(target, _targetZoom)) return;

        _autoTargetZoom = null;   // 手动滚轮接管：取消自动缩放目标，缩放由 tween 驱动
        _targetZoom = target;

        // 记录锚点（滚轮瞬间的鼠标世界坐标 + 屏幕坐标），动画期间保持该世界点不动
        _zoomAnchorWorld = GetGlobalMousePosition();
        _zoomAnchorScreen = GetViewport().GetMousePosition();

        // 从当前 Zoom 平滑动画到目标；快速连滚时 Kill 旧动画从当前值继续
        float from = Zoom.X;
        _zoomTween?.Kill();
        _zoomTween = CreateTween();
        _zoomTween.SetTrans(Tween.TransitionType.Cubic);
        _zoomTween.SetEase(Tween.EaseType.Out);
        _zoomTween.TweenMethod(
            Callable.From((float z) => ApplyZoom(z)),
            from, target, ZoomAnimationDuration);
    }

    /// <summary>应用缩放值并修正相机位置，保持锚点世界点不动</summary>
    private void ApplyZoom(float z)
    {
        Zoom = new Vector2(z, z);

        // 屏幕偏移 = 鼠标屏幕坐标 - 屏幕中心；世界偏移 = 屏幕偏移 / 缩放
        // 相机位置 = 锚点世界坐标 - 世界偏移（锚点世界点始终投影在鼠标屏幕位置）
        Vector2 screenSize = GetViewportRect().Size;
        Vector2 offset = _zoomAnchorScreen - screenSize / 2f;
        GlobalPosition = _zoomAnchorWorld - offset / z;
    }

    /// <summary>
    /// 处理鼠标拖动事件，更新摄像机位置以实现拖拽效果。
    /// </summary>
    /// <param name="mouseMotion"></param>
    private void HandleMouseDrag(InputEventMouseMotion mouseMotion)
    {
        Vector2 screenDelta = mouseMotion.Position - _dragStartScreenPos;
        GlobalPosition = _dragStartCameraPos - screenDelta * DragSensitivity / Zoom;
        GetViewport().SetInputAsHandled();
    }
}
