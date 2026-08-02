/// <summary>
/// 格子运行时属性类型（ModifyCellStatAction 目标）
/// </summary>
public enum CellStatType
{
    /// <summary>移动消耗（数值增减，可逆）</summary>
    MoveCost,

    /// <summary>可站立（布尔覆盖，不可逆）</summary>
    CanStand,

    /// <summary>可穿越（布尔覆盖，不可逆）</summary>
    CanPass,
}
