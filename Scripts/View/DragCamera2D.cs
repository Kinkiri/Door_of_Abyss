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
    /// 缩放步长，控制鼠标滚轮缩放时摄像机缩放的增量。
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

    private bool _isDragging;
    private Vector2 _dragStartScreenPos;
    private Vector2 _dragStartCameraPos;
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
                {
                    float factor = 1f + ZoomStep;
                    Zoom = new Vector2(
                        Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom),
                        Mathf.Clamp(Zoom.Y * factor, MinZoom, MaxZoom)
                    );
                    GetViewport().SetInputAsHandled();
                    break;
                }

            case MouseButton.WheelDown:
                {
                    float factor = 1f / (1f + ZoomStep);
                    Zoom = new Vector2(
                        Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom),
                        Mathf.Clamp(Zoom.Y * factor, MinZoom, MaxZoom)
                    );
                    GetViewport().SetInputAsHandled();
                    break;
                }
        }
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
