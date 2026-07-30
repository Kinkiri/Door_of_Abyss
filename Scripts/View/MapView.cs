using Godot;
using System.Collections.Generic;

/// <summary>
/// 地图渲染层：渲染基础地形 + 监听 SelectionManager 信号绘制移动/攻击高亮
/// </summary>
public partial class MapView : Node2D
{
    public static MapView Instance { get; private set; }

    // ── 基础地形渲染 ────────────────────────────────────────────────────

    [Export] public TileMapLayer BaseTerrainLayer { get; set; }

    [Export] public string CustomDataLayerName { get; set; } = "data";

    // ── 高亮渲染 ────────────────────────────────────────────────────────

    [Export] public TileMapLayer MoveMapLayer { get; set; }

    [Export] public TileMapLayer AttackMapLayer { get; set; }

    [Export] public TileMapLayer CardPreviewLayer { get; set; }

    [Export] public Vector2I MoveHighlightAtlasCoords { get; set; } = Vector2I.Zero;

    [Export] public Vector2I AttackHighlightAtlasCoords { get; set; } = new Vector2I(8, 0);

    [Export] public int HighlightSourceId { get; set; } = 0;

    // ======================================================================

    public override void _Ready()
    {
        Instance = this;

        if (MapManager.Instance != null)
            MapManager.Instance.MapUpdated += OnMapUpdated;

        SelectionManager.Instance.SelectionUpdated += OnSelectionUpdated;
    }

    public override void _ExitTree()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.MapUpdated -= OnMapUpdated;

        SelectionManager.Instance.SelectionUpdated -= OnSelectionUpdated;
    }

    // ======================================================================
    // 基础地形
    // ======================================================================

    private void OnMapUpdated()
    {
        RenderBaseTerrain();
    }

    /// <summary>根据 MapManager 的 Cell 数据渲染基础地形</summary>
    public void RenderBaseTerrain()
    {
        if (BaseTerrainLayer == null) return;

        BaseTerrainLayer.Clear();
        var map = MapManager.Instance?.Map;
        if (map == null || map.Count == 0) return;

        var lookup = BuildTileLookupFromTileSet();
        if (lookup.Count == 0) return;

        foreach (var kvp in map)
        {
            Vector2I gridPos = kvp.Key;
            BlockData block = kvp.Value.BaseBlock;
            if (block == null) continue;

            if (lookup.TryGetValue(block, out var entry))
                BaseTerrainLayer.SetCell(gridPos, entry.sourceId, entry.atlasCoords);
        }
    }

    private struct TileEntry
    {
        public int sourceId;
        public Vector2I atlasCoords;
    }

    /// <summary>
    /// 从 TileSet 反向构建 BlockData → Tile 映射。
    /// 每个瓦片在 TileSet 编辑器中绑定了 BlockData 作为自定义数据，
    /// 运行时通过此映射将 Cell 的 BlockData 反查回瓦片坐标用于渲染。
    /// 仅在 _Ready / OnMapUpdated 时执行一次，非每帧调用。
    /// </summary>
    private Dictionary<BlockData, TileEntry> BuildTileLookupFromTileSet()
    {
        var lookup = new Dictionary<BlockData, TileEntry>();
        var tileSet = BaseTerrainLayer.TileSet;
        if (tileSet == null) return lookup;

        for (int i = 0; i < tileSet.GetSourceCount(); i++)
        {
            int sourceId = tileSet.GetSourceId(i);
            var source = tileSet.GetSource(sourceId) as TileSetAtlasSource;
            if (source == null) continue;

            for (int j = 0; j < source.GetTilesCount(); j++)
            {
                Vector2I atlasCoords = source.GetTileId(j);
                var tileData = source.GetTileData(atlasCoords, 0);
                if (tileData == null) continue;

                var variant = tileData.GetCustomData(CustomDataLayerName);
                var block = variant.As<BlockData>();
                if (block == null) continue;

                lookup[block] = new TileEntry { sourceId = sourceId, atlasCoords = atlasCoords };
            }
        }

        return lookup;
    }

    // ======================================================================
    // 高亮
    // ======================================================================

    private void OnSelectionUpdated()
    {
        ClearHighlights();

        var sm = SelectionManager.Instance;
        var bm = BattleManager.Instance;

        // 放门阶段：渲染可放置区域
        if (bm?.IsPlacingDoor == true && bm.LastDoorPlaceZone != null)
        {
            RenderDoorPlaceZone(bm.LastDoorPlaceZone);
            return; // 不渲染其他高亮
        }

        if (sm.LastReachableCells != null)
            RenderMoveHighlights(sm.LastReachableCells);

        if (sm.LastAttackRange != null)
            RenderAttackRange(sm.LastAttackRange);

        if (sm.LastAttackableTargets != null)
            RenderAttackTargets(sm.LastAttackableTargets);

        // 卡牌目标预览（悬停时）
        if (sm.SelectedCard != null && sm.LastCardPreviewCells != null)
            RenderCardPreview(sm.SelectedCard, sm.LastCardPreviewCells);
    }

    private void ClearHighlights()
    {
        MoveMapLayer?.Clear();
        AttackMapLayer?.Clear();
        CardPreviewLayer?.Clear();
    }

    // ======================================================================
    // 卡牌目标预览
    // ======================================================================

    private void RenderCardPreview(Card card, HashSet<Vector2I> cells)
    {
        if (CardPreviewLayer == null) return;

        // 单位卡：渲染门的部署范围（图集坐标 0,0）
        if (card.Shape == TargetShape.SingleCell && card.Type == CardType.Unit)
            RenderDeployRange();

        // 根据 Filter 选择不同图集坐标，区分敌友
        var atlas = card.Filter switch
        {
            TargetFilter.Ally => new Vector2I(0, 0),   // 友方（治疗/增益）
            TargetFilter.Enemy => new Vector2I(16, 0),  // 敌方（伤害）
            _ => new Vector2I(8, 0),                     // 其他（召唤等）
        };

        foreach (var pos in cells)
            CardPreviewLayer.SetCell(pos, HighlightSourceId, atlas);
    }

    /// <summary>渲染所有玩家门的部署范围（并集）</summary>
    private void RenderDeployRange()
    {
        var map = MapManager.Instance?.Map;
        if (map == null) return;

        var deployCells = new HashSet<Vector2I>();
        foreach (var door in UnitManager.GetDoors(Team.Player))
        {
            if (door.UnitData is not DoorData doorData) continue;
            int range = doorData.DeployRange;
            Vector2I doorPos = door.GridPos;
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    if (System.Math.Abs(dx) + System.Math.Abs(dy) > range) continue;
                    var pos = new Vector2I(doorPos.X + dx, doorPos.Y + dy);
                    if (map.ContainsKey(pos))
                        deployCells.Add(pos);
                }
        }

        // 渲染部署范围高亮（沿用攻击高亮色）
        if (AttackMapLayer == null) return;
        var atlas = new Vector2I(16, 0);
        foreach (var pos in deployCells)
            AttackMapLayer.SetCell(pos, HighlightSourceId, atlas);
    }

    /// <summary>渲染放门区域</summary>
    private void RenderDoorPlaceZone(HashSet<Vector2I> cells)
    {
        if (AttackMapLayer == null) return;
        var atlas = new Vector2I(16, 0); // 用攻击高亮色表示可放置
        foreach (var pos in cells)
            AttackMapLayer.SetCell(pos, HighlightSourceId, atlas);
    }

    private void RenderMoveHighlights(HashSet<Vector2I> cells)
    {
        if (MoveMapLayer == null) return;
        foreach (Vector2I pos in cells)
            MoveMapLayer.SetCell(pos, HighlightSourceId, MoveHighlightAtlasCoords);
    }

    private void RenderAttackRange(HashSet<Vector2I> cells)
    {
        if (AttackMapLayer == null) return;
        foreach (Vector2I pos in cells)
            AttackMapLayer.SetCell(pos, HighlightSourceId, AttackHighlightAtlasCoords);
    }

    private void RenderAttackTargets(HashSet<Vector2I> cells)
    {
        if (AttackMapLayer == null) return;
        Vector2I targetAtlas = new Vector2I(16, 0);
        foreach (Vector2I pos in cells)
            AttackMapLayer.SetCell(pos, HighlightSourceId, targetAtlas);
    }
}
