using Godot;

/// <summary>
/// 运行时单元格，表示地图中的一个格子，包含地形数据、坐标、状态等信息
/// </summary>
public partial class Cell
{
    
    /// <summary>地块基础数据（引用自 BlockData 资源）</summary>
    public BlockData BaseBlock { get; set; }

    /// <summary>格子坐标（TileMap 坐标系）</summary>
    public Vector2I GridPos { get; set; }

    /// <summary>格子中心的世界坐标</summary>
    public Vector2 WorldPos { get; set; }

    /// <summary>当前站立在该格子上的单位</summary>
    public Unit OccupyingUnit { get; set; }

    /// <summary>当前覆盖在该格子上的环境（无则 null）</summary>
    public Environment Environment { get; set; }

    /// <summary>从 BaseBlock 拷贝过来的运行时属性</summary>
    public int MoveCost { get; set; }
    /// <summary>
    /// 是否可以站立在该格子上（例如水面或悬崖可能不可站立），不能站立的格子或许可以通过，但不能停留，该格不在移动范围内
    /// </summary>
    public bool CanStand { get; set; }
    /// <summary>
    /// 是否可以通过该格子（例如墙壁或障碍物可能不可通过），不能通过的格子一定不能站立，不能进入移动范围
    /// </summary>
    public bool CanPass { get; set; }

    public Cell() { }

    public Cell(BlockData blockData, Vector2I gridPos, Vector2 worldPos)
    {
        BaseBlock = blockData;
        GridPos = gridPos;
        WorldPos = worldPos;
        InitializeFromBlock();
    }

    /// <summary>
    /// 将 BlockData 的静态值拷贝到运行时字段
    /// </summary>
    public void InitializeFromBlock()
    {
        MoveCost = BaseBlock?.MoveCost ?? 1;
        CanStand = BaseBlock?.CanStand ?? true;
        CanPass = BaseBlock?.CanPass ?? true;
    }

    /// <summary>
    /// 获取单元格的描述信息
    /// </summary>
    public string Description =>
        $"基础地形: {BaseBlock?.BlockName}\n" +
        $"{BaseBlock?.BlockDescription}\n" +
        $"移动消耗: {MoveCost}";

    public override string ToString()
    {
        return $"[Cell {GridPos}] {BaseBlock?.BlockName} | " +
               $"体力={MoveCost} 站立={CanStand} 通过={CanPass}";
    }
}
