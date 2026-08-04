/// <summary>目标范围形状</summary>
public enum TargetShape
{
    None,
    SingleUnit,
    SingleCell,
    AreaDiamond,
    AreaSquare,
    All,

    /// <summary>十字：中心 + 上下左右各 Length 格（CellShape: CrossShape）</summary>
    Cross,

    /// <summary>叉字：中心 + 四对角各 Length 格（CellShape: XShape）</summary>
    X,

    /// <summary>射线：沿方向 Length 排 × (2×Width+1) 宽（CellShape: RayShape）</summary>
    Ray,

    /// <summary>三角形（锥形）：沿方向第 i 排宽 2i+1（CellShape: TriangleShape）</summary>
    Triangle,

    /// <summary>整行：中心所在行的左右各 Length 格（CellShape: RowShape）</summary>
    Row,

    /// <summary>整列：中心所在列的上下各 Length 格（CellShape: ColumnShape）</summary>
    Column,

    /// <summary>环形：曼哈顿距离恰为 Radius 的格子（不含内部，CellShape: RingShape）</summary>
    Ring,
}
