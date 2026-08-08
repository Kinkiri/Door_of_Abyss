using System;

/// <summary>
/// UI 面板统一接口：面板栈（PanelStack）成员协议。
/// 打开时 Open（含入场动画），关闭时 Close（含退场动画），IsOpen 供栈查询。
/// </summary>
public interface IPanel
{
    bool IsOpen { get; }
    void Open();
    void Close();
}

/// <summary>
/// 委托适配器：把无独立脚本的面板（如主界面关于/选关 PanelContainer）包装为 IPanel。
/// </summary>
public sealed class PanelAdapter : IPanel
{
    private readonly Func<bool> _isOpen;
    private readonly Action _open;
    private readonly Action _close;

    public PanelAdapter(Func<bool> isOpen, Action open, Action close)
    {
        _isOpen = isOpen;
        _open = open;
        _close = close;
    }

    public bool IsOpen => _isOpen();
    public void Open() => _open();
    public void Close() => _close();
}
