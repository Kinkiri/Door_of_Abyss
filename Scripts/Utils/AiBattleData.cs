using Godot;
using System.Collections.Generic;

/// <summary>
/// AI 战场快照（EnemyAI 构建，AiTactics 纯读；零 Manager 依赖，可单测）。
/// 每决策重建一次（前一单位行动后状态新鲜），纯数据不持有任何 Manager 引用。
/// </summary>
public class AiBattleData
{
    /// <summary>地图（格子坐标 → Cell）</summary>
    public Dictionary<Vector2I, Cell> Map;

    /// <summary>存活玩家单位（含门）</summary>
    public List<Unit> PlayerUnits;

    /// <summary>存活敌方单位（不含门；群体冲锋稀释统计用）</summary>
    public List<Unit> EnemyUnits;

    /// <summary>存活玩家门（PlayerUnits 的子集）</summary>
    public List<Unit> PlayerDoors;

    /// <summary>下回合刷怪格（标准+；简单传 null=不回避）</summary>
    public HashSet<Vector2I> SpawnCells;

    /// <summary>狡诈集火目标（可 null；死亡/回合开始由 EnemyAI 清除）</summary>
    public Unit FocusTarget;

    /// <summary>上回合移动起点（禁止移回该格，防 A↔B 来回动）</summary>
    public Vector2I? PreviousPos;

    /// <summary>决策等级</summary>
    public AiLevel Level;
}
