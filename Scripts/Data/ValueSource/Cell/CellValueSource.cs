using Godot;

/// <summary>
/// 格子坐标值源基类。与 int 版 ValueSource 平行的坐标版值源，返回 Vector2I?（null = 无有效坐标，消费方应静默跳过）。
/// 纯坐标计算（读取/偏移/步进）不做地图校验，可链式组合（如 Step → Offset），
/// 由最终消费方（召唤落点/传送落点/形状扩散中心/随机格枚举）经地图查格校验。
/// </summary>
[GlobalClass]
public abstract partial class CellValueSource : Resource
{
    /// <summary>在指定上下文中获取格子坐标；null = 无有效坐标</summary>
    public abstract Vector2I? GetCell(Context ctx);
}
