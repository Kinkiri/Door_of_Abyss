using Godot;
using System.Collections.Generic;

/// <summary>
/// 地图数据，由 MapExporter 从 TileMap 场景导出得到的静态文件
/// </summary>
[GlobalClass]
public partial class MapData : Resource
{
    /// <summary>坐标列表（Vector2I），与 Blocks 一一对应</summary>
    [Export] public Godot.Collections.Array Positions { get; set; } = new();

    /// <summary>地形模板列表（BlockData），与 Positions 一一对应</summary>
    [Export] public Godot.Collections.Array Blocks { get; set; } = new();

    /// <summary>转换为 Dictionary&lt;Vector2I, BlockData&gt; 供游戏逻辑使用</summary>
    public Dictionary<Vector2I, BlockData> ToBlockDict()
    {
        var dict = new Dictionary<Vector2I, BlockData>();
        int len = Mathf.Min(Positions.Count, Blocks.Count);
        for (int i = 0; i < len; i++)
        {
            var block = Blocks[i].As<BlockData>();
            if (block == null) continue;
            dict[(Vector2I)Positions[i]] = block;
        }
        return dict;
    }

    /// <summary>从字典设置数据（供 MapExporter 调用）</summary>
    public void SetFromDict(Dictionary<Vector2I, BlockData> source)
    {
        Positions.Clear();
        Blocks.Clear();
        foreach (var kvp in source)
        {
            Positions.Add(kvp.Key);
            Blocks.Add(kvp.Value);
        }
    }
}
