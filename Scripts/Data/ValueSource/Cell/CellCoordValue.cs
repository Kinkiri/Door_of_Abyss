using Godot;

/// <summary>坐标分量取值类型</summary>
public enum CellCoordInfo
{
    /// <summary>X 分量</summary>
    PosX,

    /// <summary>Y 分量</summary>
    PosY,
}

/// <summary>
/// 坐标分量值源：读取某坐标值源的 X/Y 分量（int），供 CompareCondition 做"X 坐标≥5"类判断。
/// 坐标来源无效时返回 DefaultValue。
/// </summary>
[GlobalClass]
public partial class CellCoordValue : ValueSource
{
    /// <summary>坐标来源（null 或无有效坐标时返回 DefaultValue）</summary>
    [Export] public CellValueSource Cell { get; set; }

    [Export] public CellCoordInfo Info { get; set; } = CellCoordInfo.PosX;

    [Export] public int DefaultValue { get; set; } = 0;

    public override int GetValue(Context ctx)
    {
        if (Cell == null) return DefaultValue;
        var pos = Cell.GetCell(ctx);
        if (pos == null) return DefaultValue;
        return Info == CellCoordInfo.PosX ? pos.Value.X : pos.Value.Y;
    }
}
