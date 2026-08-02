using Godot;

/// <summary>
/// 环境视图管理器（View 层）。
/// 订阅 EnvironmentManager 的 EnvironmentApplied/EnvironmentRemoved 事件，
/// 在环境图层（TileMapLayer）上按 EnvironmentData 填写的图集坐标 SetCell/EraseCell。
/// 事件驱动模式，与 MapView.RenderBaseTerrain 同款图层渲染。
/// 需添加到场景中（Level.tscn 的 Map 节点下），并配置 EnvironmentLayer。
/// </summary>
public partial class EnvironmentViewManager : Node
{
    public static EnvironmentViewManager Instance { get; private set; }

    /// <summary>环境图层（TileMapLayer），由用户在场景中创建并拖入（TileSet 需含环境图集）</summary>
    [Export] public TileMapLayer EnvironmentLayer { get; set; }

    public override void _Ready()
    {
        Instance = this;

        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentApplied += OnEnvironmentApplied;
            EnvironmentManager.Instance.EnvironmentRemoved += OnEnvironmentRemoved;
        }
    }

    public override void _ExitTree()
    {
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.EnvironmentApplied -= OnEnvironmentApplied;
            EnvironmentManager.Instance.EnvironmentRemoved -= OnEnvironmentRemoved;
        }
        if (Instance == this) Instance = null;
    }

    private void OnEnvironmentApplied(Cell cell, Environment env)
    {
        if (EnvironmentLayer == null)
        {
            GD.Print("[EnvironmentViewManager] EnvironmentLayer 未配置，跳过图块渲染");
            return;
        }
        if (cell == null || env?.Data == null) return;

        EnvironmentLayer.SetCell(cell.GridPos, env.Data.AtlasSourceId, env.Data.AtlasCoords);
        GD.Print($"[EnvironmentViewManager] 渲染环境图块: {env.Data.EnvironmentName} @ {cell.GridPos}");
    }

    private void OnEnvironmentRemoved(Cell cell, Environment env)
    {
        if (EnvironmentLayer == null || cell == null) return;

        EnvironmentLayer.EraseCell(cell.GridPos);
        GD.Print($"[EnvironmentViewManager] 擦除环境图块 @ {cell.GridPos}");
    }
}
