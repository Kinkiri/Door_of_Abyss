using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 单位管理器，负责管理战斗中所有单位的生成、移动和移除
/// 需添加到 Godot 的自动加载列表中作为全局单例
/// </summary>
public partial class UnitManager : Node
{
    /// <summary>全局单例引用</summary>
    public static UnitManager Instance { get; private set; }

    /// <summary>当前战场上所有活跃的单位列表</summary>
    public List<Unit> ActiveUnits { get; private set; } = new();

    /// <summary>单位生成事件（View 层订阅，负责创建 UnitView）</summary>
    public event System.Action<Unit> OnUnitSpawned;

    /// <summary>单位移除事件（View 层订阅，负责清理 UnitView 引用）</summary>
    public event System.Action<Unit> OnUnitRemoved;

    /// <summary>获取指定阵营的所有存活门</summary>
    public static IEnumerable<Unit> GetDoors(Team team)
    {
        return Instance?.ActiveUnits
            .Where(u => u.IsAlive && !u.IsDead && u.Team == team && u.Type == UnitType.Door)
            ?? Enumerable.Empty<Unit>();
    }

    public override void _Ready()
    {
        Instance = this;
    }

    public void Init() { }

    // ======================================================================
    // 放置单位
    // ======================================================================

    /// <summary>
    /// 在指定格子生成一个单位，返回生成的 UnitView；若格子不可用则返回 null
    /// </summary>
    public Unit SpawnUnit(UnitData unitData, Vector2I gridPos, Team team)
    {
        if (unitData.UnitPrefab == null)
        {
            GD.PrintErr($"UnitManager: {unitData.UnitID} 的 UnitPrefab 未赋值");
            return null;
        }

        if (!MapManager.Instance.TryGetCell(gridPos, out Cell cell))
        {
            GD.PrintErr($"UnitManager: 格子 {gridPos} 不存在");
            return null;
        }

        if (!cell.CanStand)
        {
            GD.PrintErr($"UnitManager: 格子 {gridPos} 不可站立");
            return null;
        }

        if (cell.OccupyingUnit != null)
        {
            GD.PrintErr($"UnitManager: 格子 {gridPos} 已被单位占据");
            return null;
        }

        // 创建运行时单位
        var unit = new Unit(unitData, gridPos, team);
        unit.ID = GetNextUnitID(); // 分配唯一 ID
        cell.OccupyingUnit = unit;
        cell.CanPass = false;   // 单位占据，不可穿越
        cell.CanStand = false;  // 单位占据，不可站立
        ActiveUnits.Add(unit);

        // 通知 View 层创建单位视图（事件驱动；无订阅者则无视图——逻辑层不依赖 View 层）
        OnUnitSpawned?.Invoke(unit);

        unit.UpdateUnit();

        // 注册被动效果 + 触发登场事件
        EventBus.Instance?.Subscribe(unit, unitData.PassiveEffects);
        EventBus.Instance?.Fire(EventType.OnSpawn, new Context(), subject: unit);

        // 单位出现在格子（占用从空→有）：触发环境"进入"被动（TargetCell=该格，TargetUnit=单位）
        EventBus.Instance?.Fire(EventType.OnUnitEnterCell,
            new Context { TargetCell = cell, TargetUnit = unit, SourceUnit = unit, SourceTeam = team },
            subject: unit);

        return unit;
    }

    /// <summary>
    /// 重载：直接使用 UnitLibrary 中的 ID 生成单位
    /// </summary>
    public Unit SpawnUnit(string unitID, Vector2I gridPos, Team team)
    {
        UnitData data = UnitLibrary.GetUnitByID(unitID);
        if (data == null)
        {
            GD.PrintErr($"UnitManager: 未找到 UnitID={unitID}");
            return null;
        }
        return SpawnUnit(data, gridPos, team);
    }

    // ======================================================================
    // 伤害 / 治疗 / 死亡
    // ======================================================================

    /// <summary>
    /// 释放格子占用：置 OccupyingUnit=null + 统一重算属性（基础值+环境修正）+ 触发 OnUnitLeaveCell。
    /// 仅当格子当前被指定单位占据时动作（幂等——DestroyUnit 后 RemoveUnit 重复调用安全）。
    /// </summary>
    private static void ReleaseCell(Cell cell, Unit unit)
    {
        if (cell == null || unit == null) return;
        if (cell.OccupyingUnit != unit) return;

        cell.OccupyingUnit = null;
        // 运行时 CanPass/CanStand 由 UnitManager 动态管理——
        // 单位占据时设为 false，移走时经 EnvironmentManager 统一重算（基础值+环境修正）
        EnvironmentManager.Instance?.RefreshCellProperties(cell);

        // 占用从有→空：触发环境"离开"被动（TargetCell=原格子，TargetUnit=离开的单位）
        EventBus.Instance?.Fire(EventType.OnUnitLeaveCell,
            new Context { TargetCell = cell, TargetUnit = unit, SourceUnit = unit, SourceTeam = unit.Team },
            subject: unit);
    }

    /// <summary>对单位造成伤害，HP 归零则自动移除</summary>
    public int DamageUnit(Unit unit, int damage)
    {
        if (!unit.CanBeAttacked || unit.IsDead) return 0;

        int actual = Mathf.Max(damage, 0);
        unit.CurrentHP -= actual;
        GD.Print($"UnitManager: {unit.UnitData?.UnitName} 受到 {actual} 点伤害，HP: {unit.CurrentHP}/{unit.MaxHP}");

        if (unit.CurrentHP <= 0)
        {
            unit.CurrentHP = 0;
            DestroyUnit(unit);
        }
        else
        {
            unit.UpdateUnit();
        }

        return actual;
    }

    /// <summary>治疗单位，不超过最大生命值</summary>
    public void HealUnit(Unit unit, int amount)
    {
        if (unit.IsDead) return;

        unit.CurrentHP = Mathf.Min(unit.CurrentHP + amount, unit.MaxHP);
        unit.UpdateUnit();
        GD.Print($"UnitManager: 治疗 {unit.UnitData?.UnitName}，HP: {unit.CurrentHP}/{unit.MaxHP}");
    }

    /// <summary>销毁单位（HP归零时调用），清理格子和引用</summary>
    public void DestroyUnit(Unit unit)
    {
        GD.Print($"UnitManager: {unit.UnitData?.UnitName} 死亡");

        // 先释放所在格子（亡语被动可能原地召唤新单位；RemoveUnit 内重复清理幂等保留）
        Cell deathCell = null;
        if (MapManager.Instance.TryGetCell(unit.GridPos, out deathCell))
            ReleaseCell(deathCell, unit);

        // 触发死亡事件（subject=死者；附格子/阵营，供亡语原地召唤/同阵营效果使用）
        EventBus.Instance?.Fire(EventType.OnUnitDeath,
            new Context
            {
                TargetUnit = unit,
                TargetCell = deathCell,
                SourceUnit = unit,
                SourceTeam = unit.Team,
            },
            subject: unit);

        // 任意单位死亡事件（无 subject 定向）：存活单位的被动可监听"其他单位死亡"。
        // 区别于亡语 OnUnitDeath（只触发死者自身）；本事件死者被 EventBus 存活检查排除，
        // 监听者用 TargetFilters 筛阵营/类型（如 [Shape(全体), Team(友方)] = 友方死亡时）。
        EventBus.Instance?.Fire(EventType.OnAnyUnitDeath,
            new Context
            {
                TargetUnit = unit,
                TargetCell = deathCell,
                SourceUnit = unit,
                SourceTeam = unit.Team,
            });

        RemoveUnit(unit);
    }

    // ======================================================================
    // 移除单位
    // ======================================================================

    /// <summary>
    /// 从战场上移除指定单位，清理 Cell 引用和场景节点
    /// </summary>
    public void RemoveUnit(Unit unit)
    {
        if (MapManager.Instance.TryGetCell(unit.GridPos, out Cell cell))
        {
            if (cell.OccupyingUnit == unit)
            {
                GD.Print($"UnitManager: 移除单位 {unit.ID}");
                ReleaseCell(cell, unit);
            }
        }

        ActiveUnits.Remove(unit);
        unit.IsDead = true;
        unit.UpdateUnit();

        // 通知 View 层清理视图引用（不 QueueFree——UnitView 自己播完死亡动画后销毁）
        OnUnitRemoved?.Invoke(unit);

        // 取消被动效果订阅
        EventBus.Instance?.Unsubscribe(unit);

        // 清理 Buff
        BuffManager.Instance?.RemoveAllBuffs(unit);

        // 清理装备（还原属性加成 + 取消装备被动）
        EquipmentManager.Instance?.RemoveAllEquipments(unit);
    }

    // ======================================================================
    // 移动单位
    // ======================================================================

    /// <summary>
    /// 将单位从当前格子移动到目标格子，返回移动是否成功
    /// </summary>
    public bool MoveUnit(Unit unit, Vector2I targetGridPos)
    {
        if (!MapManager.Instance.TryGetCell(targetGridPos, out Cell targetCell))
        {
            GD.PrintErr($"UnitManager: 目标格子 {targetGridPos} 不存在");
            return false;
        }

        if (!targetCell.CanStand)
        {
            GD.PrintErr($"UnitManager: 目标格子 {targetGridPos} 不可站立");
            return false;
        }

        if (targetCell.OccupyingUnit != null)
        {
            GD.PrintErr($"UnitManager: 目标格子 {targetGridPos} 已被占据");
            return false;
        }

        // 清理旧格子（释放占用 + 触发环境"离开"被动）
        if (MapManager.Instance.TryGetCell(unit.GridPos, out Cell oldCell))
            ReleaseCell(oldCell, unit);

        // 绑定新格子（占用从空→有，触发环境"进入"被动）
        unit.GridPos = targetGridPos;
        targetCell.OccupyingUnit = unit;
        targetCell.CanPass = false;   // 占据新格子
        targetCell.CanStand = false;
        EventBus.Instance?.Fire(EventType.OnUnitEnterCell,
            new Context { TargetCell = targetCell, TargetUnit = unit, SourceUnit = unit, SourceTeam = unit.Team },
            subject: unit);

        unit.UpdateUnit();
        return true;
    }

    /// <summary>
    /// 强制传送单位到指定格子，不验证 CanStand/OccupyingUnit，由调用方保证合法性。
    /// 供 MoveUnitAction 等强制位移使用。
    /// </summary>
    public void TeleportUnit(Unit unit, Vector2I targetGridPos)
    {
        // 清理旧格子（释放占用 + 触发环境"离开"被动）
        if (MapManager.Instance.TryGetCell(unit.GridPos, out Cell oldCell))
            ReleaseCell(oldCell, unit);

        // 绑定新格子（占用从空→有，触发环境"进入"被动）
        if (MapManager.Instance.TryGetCell(targetGridPos, out Cell newCell))
        {
            unit.GridPos = targetGridPos;
            newCell.OccupyingUnit = unit;
            newCell.CanPass = false;
            newCell.CanStand = false;
            EventBus.Instance?.Fire(EventType.OnUnitEnterCell,
                new Context { TargetCell = newCell, TargetUnit = unit, SourceUnit = unit, SourceTeam = unit.Team },
                subject: unit);
        }

        unit.UpdateUnit();
    }

    // ======================================================================
    // 查询
    // ======================================================================

    /// <summary>获取指定格子上的单位，没有则返回 null</summary>
    public Unit GetUnitAt(Vector2I gridPos)
    {
        if (MapManager.Instance.TryGetCell(gridPos, out Cell cell))
            return cell.OccupyingUnit;
        return null;
    }

    /// <summary>
    /// 获取下一个可用的单位 ID，确保每个单位在战场上有唯一标识
    /// </summary>
    /// <returns></returns>
    private int GetNextUnitID()
    {
        int maxID = 0;
        foreach (var unit in ActiveUnits)
        {
            if (unit.ID > maxID)
                maxID = unit.ID;
        }
        return maxID + 1;
    }

}
