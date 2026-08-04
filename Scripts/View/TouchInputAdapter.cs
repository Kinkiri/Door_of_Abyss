using Godot;
using System.Collections.Generic;

/// <summary>
/// 触摸输入适配器：补充 Godot 原生触摸→鼠标模拟覆盖不了的手势。
///
/// 分工约定：
///   - 单指点击（按下→抬起，未超阈值）交给 Godot 原生触摸→鼠标模拟
///     （project.godot 的 emulate_mouse_from_touch=true，位置天然正确，
///      SelectionManager / BattleManager 放门 / Control 按钮直接可用）
///   - 单指按下后移动超阈值  = 中键拖拽镜头（进入拖拽时先注入右键取消，
///     避免按下瞬间 Godot 模拟出的左键点击误选中单位/误出牌）
///   - 双指捏合（距离变化）   = 滚轮缩放（锚点 = 两指中点）
///   - 选中卡牌后的拖动预览由 Godot 触摸模拟的鼠标移动自动提供，无需注入
///
/// 触摸到 Control（手牌/按钮）时由 GUI 阶段先行消费，不会进入本适配器。
/// "取消"按钮由本类动态创建（等效右键，见 CreateCancelButton）。
///
/// PC 调试：按住 Ctrl + 鼠标左键模拟单指触摸（EnableDebugMouseSimulation）。
/// </summary>
[GlobalClass]
public partial class TouchInputAdapter : Node2D
{
    /// <summary>单指拖拽判定阈值（视口像素，超过则视为拖镜头而非点击）</summary>
    [Export] public float DragThreshold { get; set; } = 24f;

    /// <summary>双指捏合灵敏度：两指距离每变化多少像素 = 1 个 ZoomStep</summary>
    [Export] public float PinchSensitivity { get; set; } = 100f;

    /// <summary>PC 上是否启用 Ctrl+左键 模拟触摸（验证用）</summary>
    [Export] public bool EnableDebugMouseSimulation { get; set; } = true;

    private class TouchState
    {
        public Vector2 StartPos;
        public Vector2 LastPos;
        public bool IsDragging;
    }

    private readonly Dictionary<int, TouchState> _touches = new();

    /// <summary>本次触摸序列是否进入过双指模式（退出后直到全部抬起不再产生手势）</summary>
    private bool _wasPinching;
    private float _lastPinchDist;
    private float _pendingZoomSteps;
    private bool _middleDownInjected;

    private const int DebugTouchIndex = 99;

    public override void _Ready()
    {
        CreateCancelButton();
    }

    // ======================================================================
    // 输入：真实触摸（安卓）走 _UnhandledInput；PC 调试走 _Input
    // ======================================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch touch:
                if (touch.Pressed)
                    OnTouchDown(touch.Position, (int)touch.Index);
                else
                    OnTouchUp((int)touch.Index);
                GetViewport().SetInputAsHandled();
                break;

            case InputEventScreenDrag drag:
                OnTouchDrag((int)drag.Index, drag.Position);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _Input(InputEvent @event)
    {
        // PC 调试：Ctrl+左键 模拟单指触摸（注入的鼠标事件 CtrlPressed=false，不会循环）
        if (!EnableDebugMouseSimulation || !IsDesktop()) return;

        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left
                                              && mb.Pressed && mb.CtrlPressed:
                GetViewport().SetInputAsHandled();
                OnTouchDown(mb.Position, DebugTouchIndex);
                break;

            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left
                                              && !mb.Pressed && mb.CtrlPressed
                                              && _touches.ContainsKey(DebugTouchIndex):
                GetViewport().SetInputAsHandled();
                OnTouchUp(DebugTouchIndex);
                break;

            case InputEventMouseMotion mm when mm.CtrlPressed
                                              && _touches.ContainsKey(DebugTouchIndex):
                OnTouchDrag(DebugTouchIndex, mm.Position);
                break;
        }
    }

    private static bool IsDesktop()
    {
        return OS.HasFeature("windows") || OS.HasFeature("macos") || OS.HasFeature("linux");
    }

    // ======================================================================
    // 手势状态机
    // ======================================================================

    private void OnTouchDown(Vector2 pos, int index)
    {
        if (_touches.ContainsKey(index)) return;

        // 第二指落下 → 结束单指拖拽并进入捏合模式
        if (_touches.Count >= 1)
        {
            _wasPinching = true;
            EndDrag();
        }

        _touches[index] = new TouchState { StartPos = pos, LastPos = pos };

        if (_touches.Count == 2)
            _lastPinchDist = DistanceBetweenTouches();
    }

    private void OnTouchDrag(int index, Vector2 pos)
    {
        if (!_touches.TryGetValue(index, out var state)) return;
        state.LastPos = pos;

        // 双指捏合 → 滚轮缩放
        if (_touches.Count == 2)
        {
            float dist = DistanceBetweenTouches();
            float delta = dist - _lastPinchDist;
            _lastPinchDist = dist;

            _pendingZoomSteps += delta / PinchSensitivity;
            while (_pendingZoomSteps >= 1f)
            {
                InjectMouseButton(MouseButton.WheelUp, true, CenterOfTouches());
                _pendingZoomSteps -= 1f;
            }
            while (_pendingZoomSteps <= -1f)
            {
                InjectMouseButton(MouseButton.WheelDown, true, CenterOfTouches());
                _pendingZoomSteps += 1f;
            }
            return;
        }

        // 单指：超过阈值 → 进入拖拽（中键拖镜头）
        if (!state.IsDragging && state.StartPos.DistanceTo(pos) >= DragThreshold)
        {
            state.IsDragging = true;

            // 按下瞬间 Godot 已模拟出左键点击（可能选中单位/误出牌）→ 注入右键取消
            InjectMouseButton(MouseButton.Right, true, pos);
            InjectMouseButton(MouseButton.Right, false, pos);

            _middleDownInjected = true;
            InjectMouseButton(MouseButton.Middle, true, pos);
        }

        // 拖拽中：DragCamera2D 依赖 Motion 事件驱动镜头
        if (state.IsDragging)
            InjectMouseMotion(pos);
    }

    private void OnTouchUp(int index)
    {
        if (!_touches.TryGetValue(index, out var state)) return;
        _touches.Remove(index);

        // 曾进入双指模式：本次抬起不产生任何手势，全部抬起后复位
        if (_wasPinching)
        {
            if (_touches.Count == 0)
            {
                _wasPinching = false;
                _pendingZoomSteps = 0;
                EndDrag();
            }
            return;
        }

        // 拖拽结束：中键抬起（点击由 Godot 原生触摸模拟处理，此处不注入）
        if (state.IsDragging)
        {
            InjectMouseButton(MouseButton.Middle, false, state.LastPos);
            _middleDownInjected = false;
        }
    }

    /// <summary>结束中键拖拽（双指介入/手指全部抬起时）</summary>
    private void EndDrag()
    {
        if (_middleDownInjected)
        {
            InjectMouseButton(MouseButton.Middle, false, Vector2.Zero);
            _middleDownInjected = false;
        }
        foreach (var state in _touches.Values)
            state.IsDragging = false;
    }

    private float DistanceBetweenTouches()
    {
        var a = GetFirstTouch();
        var b = GetLastTouch();
        return a.DistanceTo(b);
    }

    private Vector2 CenterOfTouches()
    {
        var a = GetFirstTouch();
        var b = GetLastTouch();
        return (a + b) / 2f;
    }

    private Vector2 GetFirstTouch()
    {
        foreach (var state in _touches.Values)
            return state.LastPos;
        return Vector2.Zero;
    }

    private Vector2 GetLastTouch()
    {
        Vector2 last = Vector2.Zero;
        foreach (var state in _touches.Values)
            last = state.LastPos;
        return last;
    }

    // ======================================================================
    // 事件注入
    // ======================================================================

    private void InjectMouseMotion(Vector2 pos)
    {
        GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = pos,
            GlobalPosition = pos,
        });
    }

    private void InjectMouseButton(MouseButton button, bool pressed, Vector2 pos)
    {
        InjectMouseMotion(pos); // 先更新鼠标位置，保证 GetGlobalMousePosition() 正确
        GetViewport().PushInput(new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = pressed,
            Position = pos,
            GlobalPosition = pos,
        });
    }

    // ======================================================================
    // 取消按钮（等效右键）
    // ======================================================================

    private void CreateCancelButton()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var btn = new Button
        {
            Text = "✕",
            TooltipText = "取消选中",
        };

        // 左上角常驻（RoundInfoPanel 在右上角，避免遮挡）
        btn.OffsetLeft = 10f;
        btn.OffsetTop = 10f;
        btn.OffsetRight = 58f;
        btn.OffsetBottom = 58f;

        btn.Pressed += () => SelectionManager.Instance?.ClearSelection();
        layer.AddChild(btn);

        // 仅移动平台显示（桌面用右键取消）；调试模拟时也显示便于验证
        btn.Visible = OS.HasFeature("mobile") || EnableDebugMouseSimulation;
    }
}
