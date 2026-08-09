using Godot;
using System.Collections.Generic;

/// <summary>
/// 网格地图管理器，负责从 TileMap 或 MapData 解析所有地块并构建运行时 Cell 字典
/// 需添加到 Godot 的自动加载列表中作为全局单例
/// </summary>
[GlobalClass]
public partial class MapManager : Node2D
{
    /// <summary>全局单例引用</summary>
    public static MapManager Instance { get; private set; }

    /// <summary>需要解析的 BaseMapLayer 节点（一个 BaseMapLayer 对应一个图层）
    /// 这个是基础图层</summary>
    [Export] public TileMapLayer BaseMapLayer { get; set; }

    /// <summary>TileSet 自定义数据层的名称，需与 TileSet 编辑器中设定的名称一致</summary>
    [Export] public string CustomDataLayerName { get; set; } = "data";

    /// <summary>所有已解析的 Cell，key = 格子坐标</summary>
    public Dictionary<Vector2I, Cell> Map { get; private set; } = new();

    /// <summary>原始地图数据的副本，用于重置地图</summary>
    public Dictionary<Vector2I, Cell> OriginalMap { get; private set; }

    /// <summary>地图数据更新时触发（初始化 / LoadFromMapData 等）</summary>
    public event System.Action MapUpdated;

    // ======================================================================
    // 生命周期
    // ======================================================================

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void Init() { }

    // ======================================================================
    // 地图加载
    // ======================================================================

    /// <summary>
    /// 遍历 BaseMapLayer 的所有已使用格子，读取 BlockData 并构建 Cell 字典
    /// </summary>
    private void ParseAllCells()
    {
        if (BaseMapLayer == null)
        {
            GD.PrintErr("MapManager: BaseMapLayer 为空，无法解析地图");
            return;
        }

        TileSet tileSet = BaseMapLayer.TileSet;
        if (tileSet == null)
        {
            GD.PrintErr("MapManager: TileSet 为空，无法解析地图");
            return;
        }

        Map.Clear();

        int count = 0;
        foreach (Vector2I cellPos in BaseMapLayer.GetUsedCells())
        {
            TileData tileData = BaseMapLayer.GetCellTileData(cellPos);
            if (tileData == null)
                continue;

            Variant customData = tileData.GetCustomData(CustomDataLayerName);
            BlockData blockData = customData.As<BlockData>();

            if (blockData == null)
            {
                GD.PrintErr($"MapManager: 格子 {cellPos} 的 BlockData 为空，跳过");
                continue;
            }

            Vector2 localPos = BaseMapLayer.MapToLocal(cellPos);
            Vector2 worldPos = BaseMapLayer.ToGlobal(localPos);

            var cell = new Cell(blockData, cellPos, worldPos);
            Map[cellPos] = cell;
            count++;
        }
        OriginalMap = new Dictionary<Vector2I, Cell>(Map);
        GD.Print($"MapManager: 地图解析完成，共 {count} 个地块");
        MapUpdated?.Invoke();
    }

    /// <summary>
    /// 从 MapData Resource 加载地图，替换当前地图数据
    /// </summary>
    public void LoadFromMapData(MapData data)
    {
        if (BaseMapLayer == null)
        {
            GD.PrintErr("MapManager: BaseMapLayer 为空，无法计算世界坐标");
            return;
        }

        var blockDict = data.ToBlockDict();
        Map.Clear();

        foreach (var kvp in blockDict)
        {
            Vector2I gridPos = kvp.Key;
            BlockData block = kvp.Value;

            Vector2 localPos = BaseMapLayer.MapToLocal(gridPos);
            Vector2 worldPos = BaseMapLayer.ToGlobal(localPos);

            var cell = new Cell(block, gridPos, worldPos);
            Map[gridPos] = cell;
        }

        OriginalMap = new Dictionary<Vector2I, Cell>(Map);
        GD.Print($"MapManager: 从 MapData 加载地图完成，共 {Map.Count} 个地块");
        MapUpdated?.Invoke();

        // 初始化预置环境（环境瓦片化：MapData.Environment* → EnvironmentManager 静默施加）
        EnvironmentManager.Instance?.LoadPresetEnvironments(data);
    }

    // ======================================================================
    // 对外接口
    // ======================================================================

    /// <summary>尝试通过格子坐标获取 Cell</summary>
    public bool TryGetCell(Vector2I gridPos, out Cell cell)
    {
        if (Map.TryGetValue(gridPos, out cell))
        {
            return true;
        }
        cell = null;
        return false;
    }

    /// <summary>判断指定坐标是否存在 Cell</summary>
    public bool HasCell(Vector2I gridPos)
    {
        return Map.ContainsKey(gridPos);
    }

    /// <summary>通过世界坐标获取对应的 Cell</summary>
    public Cell GetCellFromWorldPos(Vector2 worldPos)
    {
        Vector2I gridPos = BaseMapLayer.LocalToMap(BaseMapLayer.ToLocal(worldPos));
        TryGetCell(gridPos, out Cell cell);
        return cell;
    }

    /// <summary>网格坐标 → 世界坐标（像素），用于定位 UnitView 位置</summary>
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        return BaseMapLayer.ToGlobal(BaseMapLayer.MapToLocal(gridPos));
    }
    /// <summary>世界坐标（像素）→ 网格坐标，用于处理鼠标点击输入</summary>
    public Vector2I WorldToGrid(Vector2 worldPos)
    {
        return BaseMapLayer.LocalToMap(BaseMapLayer.ToLocal(worldPos));
    }
}
