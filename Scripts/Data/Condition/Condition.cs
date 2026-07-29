using Godot;

/// <summary>
/// 条件基类。所有具体条件继承此类，实现 IsMet()。
/// 配合 AndCondition/OrCondition/NotCondition 支持任意嵌套。
/// </summary>
[GlobalClass]
public abstract partial class Condition : Resource
{
    /// <summary>判断条件是否满足</summary>
    public abstract bool IsMet(Context ctx);
}
