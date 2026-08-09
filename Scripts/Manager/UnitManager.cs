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

    /// <summary>单位移动事件（玩家/AI 普通移动共用 MoveUnit 入口；强制位移走 TeleportUnit 不触发）</summary>
    public event System.Action<Unit> OnUnitMoved;

    /// <summary>单位受到伤害事件（实际伤害 &gt; 0；View 层订阅——浮动数字）</summary>
    public event System.Action<Unit, int> OnUnitDamaged;

    /// <summary>单位受到治疗事件（实际治疗量 &gt; 0；View 层订阅——浮动数字）</summary>
    public event System.Action<Unit, int> OnUnitHealed;

    /// <summary>获取指定阵营的所有存活门</summary>
    public static IEnumerable<Unit> GetDoors(Team team)
    {
        return Instance?.ActiveUnits
            .Where(u => u.IsAlive && !u.IsDead && u.Team == team && u.Type == UnitType.门)
            ?? Enumerable.Empty<Unit>();
    }

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

        // 注册被动效果 + 触发登场事件（载荷：SourceUnit=登场单位、TargetCell=登场格，供手牌被动识别"谁登场/在哪里"）
        EventBus.Instance?.Subscribe(unit, unitData.PassiveEffects);
        EventBus.Instance?.Fire(EventType.OnSpawn, new Context
        {
            SourceUnit = unit,
            SourceTeam = team,
            TargetCell = cell,
        }, instigator: unit);

        // 单位出现在格子（占用从空→有）：触发环境"进入"被动（TargetCell=该格，TargetUnit=单位）
        EventBus.Instance?.Fire(EventType.OnUnitEnterCell,
            new Context { TargetCell = cell, TargetUnit = unit, SourceUnit = unit, SourceTeam = team },
            instigator: unit);

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

    /// <summary>单位变身事件（View 层订阅，刷新 UnitView 的模板显示）</summary>
    public event System.Action<Unit> OnUnitTransformed;

    /// <summary>
    /// 单位变身：换模板 + 强制重置为模板状态（满血）+ 清除一切 buff/装备 + 换被动订阅。
    /// 语义=完全重置：变身前生效中的 buff/装备（属性加成、被动订阅、视图图标）全部清除，
    /// 旧模板被动退订，新模板被动生效；位置与阵营不变。
    /// buff 走标准移除路径（RemoveBuffAction→RemoveBuff）：执行 OnExpireActions、触发
    /// OnBuffRemoved 被动事件与 BuffRemoved 视图事件；CanBeChanged=false 的固定 buff 保留不清。
    /// </summary>
    public void TransformUnit(Unit unit, UnitData newData)
    {
        if (unit == null || newData == null) return;
        if (unit.IsDead) return;

        GD.Print($"[Transform] 开始变身: {unit.UnitData?.UnitName}(ID={unit.ID}) → {newData.UnitName}");

        // ① 取消旧订阅（原生被动 + buff/装备被动一并清除）
        EventBus.Instance?.Unsubscribe(unit);
        GD.Print("[Transform] ① 取消旧订阅完成");

        // ② 重置：清除一切 buff/装备
        // buff 逐个走标准移除（还原加成 + 退订 + OnExpireActions + OnBuffRemoved 事件 + 视图销毁；
        // CanBeChanged=false 的固定 buff 被 RemoveBuffAction 拒绝，保留不清）
        var buffs = BuffManager.Instance?.GetBuffs(unit);
        GD.Print($"[Transform] ② 待移除 buff 数: {buffs?.Count ?? -1}");
        if (buffs != null)
        {
            foreach (var buff in buffs)
            {
                GD.Print($"[Transform] ② 执行 RemoveBuffAction: BuffID={buff.Data.BuffID}");
                new RemoveBuffAction { BuffID = buff.Data.BuffID }
                    .Execute(new Context { TargetUnit = unit });
            }
        }
        EquipmentManager.Instance?.RemoveAllEquipments(unit);
        GD.Print("[Transform] ② 装备清理完成");

        // ③ 换模板 + 强制刷新运行时属性（全部按新模板，满血）
        unit.UnitData = newData;
        unit.InitializeFromData();
        GD.Print($"[Transform] ③ 换模板完成: MaxHP={unit.MaxHP} CurrentHP={unit.CurrentHP}");

        // ④ 订阅新模板的被动效果
        EventBus.Instance?.Subscribe(unit, newData.PassiveEffects);
        GD.Print($"[Transform] ④ 订阅新被动完成: {newData.PassiveEffects?.Length ?? 0} 条");

        // ⑤ 通知视图刷新 + 触发变身事件（无 subject 定向：所有存活单位被动可监听"单位变身"，
        //    监听者用 TargetFilters 筛目标，如 [Shape(全体), Target(事件另一方)] = 变身者）
        unit.UpdateUnit();
        GD.Print("[Transform] ⑤ UpdateUnit 完成");

        OnUnitTransformed?.Invoke(unit);
        GD.Print($"[Transform] ⑥ OnUnitTransformed?.Invoke 完成，订阅者数={OnUnitTransformed?.GetInvocationList().Length ?? 0}");
        EventBus.Instance?.Fire(EventType.OnUnitTransformed,
            new Context { TargetUnit = unit, EventOtherUnit = unit, SourceUnit = unit, SourceTeam = unit.Team });
        GD.Print("[Transform] ⑦ Fire(OnUnitTransformed) 完成");
    }

    // ======================================================================
    // 伤害 / 治疗 / 死亡
    // ======================================================================

    /// <summary>
    /// 释放格子占用：置 OccupyingUnit=null + 统一重算属性（基础值+环境修正）+ 触发 OnUnitLeaveCell。
    /// 仅当格子当前被指定单位占据时动作（幂等——DestroyUnit 后 RemoveUnit 重复调用安全）。
    /// destCell：移动的目标格子（用于"同环境内移动"判断，对面环境与本环境 ID 相同则不触发离开）；
    /// 死亡/移除等无目标格场景不传（null → 起点格有环境则触发离开）。
    /// </summary>
    private static void ReleaseCell(Cell cell, Unit unit, Cell destCell = null)
    {
        if (cell == null || unit == null) return;
        if (cell.OccupyingUnit != unit) return;

        cell.OccupyingUnit = null;
        // 运行时 CanPass/CanStand 由 UnitManager 动态管理——
        // 单位占据时设为 false，移走时经 EnvironmentManager 统一重算（基础值+环境修正）
        EnvironmentManager.Instance?.RefreshCellProperties(cell);

        // 占用从有→空：触发环境"离开"被动（TargetCell=原格子，SourceCell=目标格子，TargetUnit=离开的单位）
        EventBus.Instance?.Fire(EventType.OnUnitLeaveCell,
            new Context { TargetCell = cell, TargetUnit = unit, SourceUnit = unit, SourceTeam = unit.Team, SourceCell = destCell },
            instigator: unit);
    }

    /// <summary>对单位造成伤害，HP 归零则自动移除；返回实际伤害量</summary>
    public int DamageUnit(Unit unit, int damage)
    {
        if (!unit.CanBeAttacked || unit.IsDead) return 0;
        // 统一走 ApplyRawHPChange（clamp/事件/致死/刷新 全收敛于此）
        return -ApplyRawHPChange(unit, -Mathf.Max(damage, 0), lethal: true);
    }

    /// <summary>治疗单位，不超过最大生命值；返回实际治疗量</summary>
    public int HealUnit(Unit unit, int amount)
    {
        if (unit.IsDead) return 0;
        return ApplyRawHPChange(unit, Mathf.Max(amount, 0), lethal: false);
    }

    /// <summary>
    /// 统一 HP 变化入口（所有扣血/回血都收敛于此，避免"直接改 CurrentHP + 各处补丁反馈"）。
    /// 负责：钳制到 [0, MaxHP]（MaxHP 保底 0）、按正负触发 OnUnitDamaged/OnUnitHealed
    /// （浮动数字反馈；致死时在 DestroyUnit 前发出，UnitView 锚点仍有效）、可选致死销毁、UpdateUnit。
    /// 返回实际变化量（正=回血，负=扣血，0=无变化）。
    /// </summary>
    public int ApplyRawHPChange(Unit unit, int delta, bool lethal)
    {
        if (unit == null || unit.IsDead) return 0;

        int oldHP = unit.CurrentHP;
        unit.CurrentHP = Mathf.Clamp(oldHP + delta, 0, Mathf.Max(unit.MaxHP, 0));
        int actual = unit.CurrentHP - oldHP;

        if (actual > 0) OnUnitHealed?.Invoke(unit, actual);
        else if (actual < 0) OnUnitDamaged?.Invoke(unit, -actual);

        if (lethal && unit.CurrentHP <= 0)
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
            instigator: unit);

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

        // 清理旧格子（释放占用 + 触发环境"离开"被动；destCell=目标格，用于同环境移动判断）
        if (MapManager.Instance.TryGetCell(unit.GridPos, out Cell oldCell))
            ReleaseCell(oldCell, unit, targetCell);

        // 绑定新格子（占用从空→有，触发环境"进入"被动；SourceCell=旧格，用于同环境移动判断）
        unit.GridPos = targetGridPos;
        targetCell.OccupyingUnit = unit;
        targetCell.CanPass = false;   // 占据新格子
        targetCell.CanStand = false;
        EventBus.Instance?.Fire(EventType.OnUnitEnterCell,
            new Context { TargetCell = targetCell, TargetUnit = unit, SourceUnit = unit, SourceTeam = unit.Team, SourceCell = oldCell },
            instigator: unit);

        unit.UpdateUnit();
        OnUnitMoved?.Invoke(unit);
        return true;
    }

    /// <summary>
    /// 强制传送单位到指定格子，不验证 CanStand/OccupyingUnit，由调用方保证合法性。
    /// 供 MoveUnitAction 等强制位移使用。
    /// </summary>
    public void TeleportUnit(Unit unit, Vector2I targetGridPos)
    {
        // 提前解析目标格子（用于"同环境移动"判断与绑定）
        MapManager.Instance.TryGetCell(targetGridPos, out Cell newCell);

        // 清理旧格子（释放占用 + 触发环境"离开"被动；destCell=目标格，用于同环境移动判断）
        if (MapManager.Instance.TryGetCell(unit.GridPos, out Cell oldCell))
            ReleaseCell(oldCell, unit, newCell);

        // 绑定新格子（占用从空→有，触发环境"进入"被动；SourceCell=旧格，用于同环境移动判断）
        if (newCell != null)
        {
            unit.GridPos = targetGridPos;
            newCell.OccupyingUnit = unit;
            newCell.CanPass = false;
            newCell.CanStand = false;
            EventBus.Instance?.Fire(EventType.OnUnitEnterCell,
                new Context { TargetCell = newCell, TargetUnit = unit, SourceUnit = unit, SourceTeam = unit.Team, SourceCell = oldCell },
                instigator: unit);
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
