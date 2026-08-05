using Godot;

/// <summary>
/// 地图导出工具——运行时按 F5 导出当前场景 TileMapLayer 为 MapData Resource
/// 挂到场景任意节点上即可
/// </summary>
public partial class MapExporter : Node
{
    [Export] public TileMapLayer SourceLayer { get; set; }

    /// <summary>环境图层（可选）：导出时读取该层瓦片绑定的 EnvironmentData，存入 MapData.Environment*</summary>
    [Export] public TileMapLayer EnvironmentSourceLayer { get; set; }

    [Export] public string CustomDataLayerName { get; set; } = "data";

    [Export] public string OutputDir { get; set; } = "res://Resource/Data/Maps/";

    [Export] public string FileName { get; set; } = "";

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.F5)
        {
            DoExport();
        }
    }

    private void DoExport()
    {
        if (SourceLayer == null)
        {
            GD.PrintErr("[MapExporter] SourceLayer 未指定");
            return;
        }

        var usedCells = SourceLayer.GetUsedCells();
        var dict = new System.Collections.Generic.Dictionary<Vector2I, BlockData>();

        foreach (Vector2I cellPos in usedCells)
        {
            var tileData = SourceLayer.GetCellTileData(cellPos);
            if (tileData == null) continue;

            var variant = tileData.GetCustomData(CustomDataLayerName);
            if (variant.VariantType != Variant.Type.Object) continue;

            // 运行时 Variant.As<BlockData>() 正常工作
            var blockData = variant.As<BlockData>();
            if (blockData == null) continue;

            dict[cellPos] = blockData;
        }

        if (dict.Count == 0)
        {
            GD.PrintErr("[MapExporter] 未导出任何有效格子");
            return;
        }

        var mapData = new MapData();
        mapData.SetFromDict(dict);

        // 导出环境层：瓦片 custom data 绑定的 EnvironmentData（环境瓦片化）
        if (EnvironmentSourceLayer != null)
        {
            var envDict = new System.Collections.Generic.Dictionary<Vector2I, EnvironmentData>();
            foreach (Vector2I cellPos in EnvironmentSourceLayer.GetUsedCells())
            {
                var tileData = EnvironmentSourceLayer.GetCellTileData(cellPos);
                if (tileData == null) continue;

                var variant = tileData.GetCustomData(CustomDataLayerName);
                if (variant.VariantType != Variant.Type.Object) continue;

                var envData = variant.As<EnvironmentData>();
                if (envData == null) continue;

                envDict[cellPos] = envData;
            }
            mapData.SetEnvironmentDict(envDict);
            if (envDict.Count > 0)
                GD.Print($"[MapExporter] 导出环境 {envDict.Count} 格");
        }

        string name = string.IsNullOrEmpty(FileName) ? SourceLayer.Name : FileName;
        string basePath = $"{OutputDir.TrimEnd('/')}/{name}";
        string fullPath = $"{basePath}.tres";

        // 重名处理：如果文件已存在，自动加数字后缀
        int suffix = 1;
        while (ResourceLoader.Exists(fullPath))
        {
            fullPath = $"{basePath}_{suffix}.tres";
            suffix++;
        }

        ResourceSaver.Save(mapData, fullPath);
        GD.Print($"[MapExporter] 导出完成：{fullPath}，共 {dict.Count} 个格子");
    }
}
