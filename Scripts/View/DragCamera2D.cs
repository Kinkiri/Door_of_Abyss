using Godot;

namespace DoorofAbbyss.UI;

[GlobalClass]
public partial class DragCamera2D : Camera2D
{
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

    public override void _Ready()
    {
        _targetZoom = Zoom.X;

        // Manager 节点在场景树中先于本节点 _Ready，延迟到帧尾再订阅，避免 Instance 为 null
        CallDeferred(nameof(SubscribeFollow));
    }

    public override void _ExitTree()
    {
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
    }

    private void UnsubscribeFollow()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectionUpdated -= OnSelectionUpdated;
        if (BattleManager.Instance != null)
            BattleManager.Instance.UnitActed -= OnUnitActed;
        if (UnitManager.Instance != null)
            UnitManager.Instance.OnUnitRemoved -= OnUnitRemoved;
    }

    // ======================================================================
    // 跟随（非线性丝滑：每帧指数平滑逼近目标，帧率无关、无过冲）
    // ======================================================================

    /// <summary>选中状态变化：选中单位 → 聚焦；取消选中 → 停止跟随</summary>
    private void OnSelectionUpdated()
    {
        if (!EnableFollow || !FollowOnSelect) return;

        var unit = SelectionManager.Instance?.SelectedUnit;
        if (unit != null)
            SetFollowTarget(unit);
        else
            ClearFollow();
    }

    /// <summary>任意单位（玩家/AI）完成移动或攻击：跟随行动单位（优先级高于选中）</summary>
    private void OnUnitActed(Unit unit)
    {
        if (!EnableFollow || !FollowOnAct) return;
        if (unit != null)
            SetFollowTarget(unit);
    }

    /// <summary>跟随单位被移除：停止跟随</summary>
    private void OnUnitRemoved(Unit unit)
    {
        if (unit == _followUnit)
            ClearFollow();
    }

    private void SetFollowTarget(Unit unit)
    {
        var view = UnitViewManager.Instance?.GetUnitView(unit);
        _followUnit = unit;
        _followTarget = view;   // 无视图（测试环境等）时为 null，不跟随
    }

    private void ClearFollow()
    {
        _followUnit = null;
        _followTarget = null;
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

        // 非线性指数平滑：起始快、接近慢
        float t = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(_followTarget.GlobalPosition, t);
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
