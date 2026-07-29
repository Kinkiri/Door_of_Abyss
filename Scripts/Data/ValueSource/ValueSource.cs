using Godot;

/// <summary>
/// 值源基类。所有数值来源继承此类，支持常量/变量/公式嵌套。
/// </summary>
[GlobalClass]
public abstract partial class ValueSource : Resource
{
    /// <summary>在指定上下文中获取数值</summary>
    public abstract int GetValue(Context ctx);
}
