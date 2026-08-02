/// <summary>
/// 格子布尔属性三态覆盖：不修改 / 强制 true / 强制 false。
/// Godot 不支持导出 bool?，用三态枚举表达"未配置=不修改"。
/// </summary>
public enum CellPropertyOverride
{
    /// <summary>不修改（沿用基础地形值）</summary>
    Unchanged,

    /// <summary>强制 true</summary>
    ForceTrue,

    /// <summary>强制 false</summary>
    ForceFalse,
}
