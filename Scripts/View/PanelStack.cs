using System.Collections.Generic;

/// <summary>
/// UI 面板栈（先进后出）：统一管理面板互斥与 Esc 关闭。
/// 面板在 Open 时 Push、Close 时 Pop；Esc 只关闭栈顶（HandleEscape），
/// 多层面板（如 暂停→设置）按打开逆序逐个关闭。纯静态，无 Godot 依赖。
/// 输入入口：主界面 MainMenu._UnhandledInput / 战斗 PauseMenu._UnhandledInput 转发。
/// </summary>
public static class PanelStack
{
    private static readonly List<IPanel> _stack = new();

    public static bool AnyOpen => _stack.Count > 0;

    /// <summary>入栈（同面板重复 Push 先移除保证唯一，面板保持单一实例）</summary>
    public static void Push(IPanel panel)
    {
        _stack.Remove(panel);
        _stack.Add(panel);
    }

    /// <summary>出栈（面板关闭时调用；允许移除栈中任意位置，安全幂等）</summary>
    public static void Pop(IPanel panel) => _stack.Remove(panel);

    /// <summary>清空栈（场景入口调用）。残留条目可能指向已释放节点，不调用 Close 直接丢弃</summary>
    public static void Clear() => _stack.Clear();

    /// <summary>关闭栈顶面板并出栈；栈空返回 false（调用方自行决定兜底行为，如打开暂停）</summary>
    public static bool HandleEscape()
    {
        if (_stack.Count == 0) return false;
        var top = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        top.Close();
        return true;
    }
}
