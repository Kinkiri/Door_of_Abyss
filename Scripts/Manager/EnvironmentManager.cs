using Godot;
using System.Collections.Generic;

/// <summary>
/// 环境管理器，管理地图上所有格子的环境生命周期。
/// 负责施加、替换式覆盖、移除、回合倒计时，以及格子属性修正（基础值+环境）的统一重算。
/// 对齐 BuffManager 模式：Manager 只发事件（EnvironmentApplied/EnvironmentRemoved），View 层订阅渲染。
/// </summary>
public partial class EnvironmentManager : Node
{
    public static EnvironmentManager Instance { get; private set; }

    /// <summary>格子 → 环境实例（Cell.Environment 同步持有引用，字典用于回合倒计时遍历）</summary>
    private readonly Dictionary<Cell, Environment> _activeEnvironments = new();

    /// <summary>环境施加事件（View 层订阅，渲染环境图层图块）</summary>
    public event System.Action<Cell, Environment> EnvironmentApplied;

    /// <summary>环境移除事件（View 层订阅，擦除环境图层图块）</summary>
    public event System.Action<Cell, Environment> EnvironmentRemoved;

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
    // 预置环境（关卡地图瓦片化环境，MapData 导出）
    // ======================================================================

    /// <summary>
    /// 加载关卡预置环境：遍历 MapData 中导出的环境瓦片数据，逐格静默施加。
    /// 在战斗初始化（加载地图后、放门阶段前）调用，此后与动态施加的环境走同一生命周期。
    /// </summary>
    public void LoadPresetEnvironments(MapData data)
    {
        if (data == null) return;

        int count = 0;
        foreach (var kvp in data.ToEnvironmentDict())
        {
            if (MapManager.Instance?.TryGetCell(kvp.Key, out Cell cell) == true && cell.Environment == null)
            {
                ApplyEnvironment(cell, kvp.Value, null);
                count++;
            }
        }
        if (count > 0)
            GD.Print($"[EnvironmentManager] 加载预置环境 {count} 格");
    }

    // ======================================================================
    // 施加环境
    // ======================================================================

    /// <summary>
    /// 对目标格子施加环境。同格已有环境 → 先完整还原旧环境（替换式覆盖），
    /// 再应用新环境（属性修正 + OnApplyActions + 订阅被动）。
    /// </summary>
    public void ApplyEnvironment(Cell cell, EnvironmentData data, Unit sourceUnit)
    {
        if (cell == null || data == null) return;

        GD.Print($"[EnvironmentManager] ApplyEnvironment: {data.EnvironmentName} @ {cell.GridPos} " +
                 $"duration={data.Duration} moveCostDelta={data.MoveCostDelta} " +
                 $"actionsCount={data.OnApplyActions?.Length}");

        // 替换式覆盖：先完整移除旧环境（还原属性 + 取消被动 + OnExpireActions）
        if (cell.Environment != null)
            RemoveEnvironment(cell);

        var env = new Environment(data, cell, sourceUnit);
        cell.Environment = env;
        _activeEnvironments[cell] = env;

        // 应用格子属性修正（基础值 + 环境）
        RefreshCellProperties(cell);

        // 执行施加动作（TargetCell=环境格子，TargetUnit=格子上单位）
        if (data.OnApplyActions != null && data.OnApplyActions.Length > 0)
        {
            var ctx = new Context { TargetCell = cell, TargetUnit = cell.OccupyingUnit, SourceUnit = sourceUnit };
            foreach (var action in data.OnApplyActions)
                action.Execute(ctx);
        }

        // 注册被动效果（带 tag 以便移除时单独清理；tag 含格子坐标避免多格同名环境互删订阅）
        if (data.PassiveEffects != null && data.PassiveEffects.Length > 0)
        {
            string tag = $"env_{data.EnvironmentID}_{cell.GridPos}";
            EventBus.Instance?.Subscribe(env, data.PassiveEffects, tag);
        }

        // 触发事件
        EventBus.Instance?.Fire(EventType.OnEnvironmentApplied,
            new Context { TargetCell = cell, SourceUnit = sourceUnit, SourceTeam = sourceUnit?.Team ?? Team.Neutral });

        // 通知 View 层渲染环境图块（事件驱动）
        EnvironmentApplied?.Invoke(cell, env);

        GD.Print($"[EnvironmentManager] 施加: {data.EnvironmentName} @ {cell.GridPos} 持续{data.Duration}回合");
    }

    // ======================================================================
    // 移除环境
    // ======================================================================

    /// <summary>
    /// 移除指定格子的环境：
    ///   1) 解除挂载并还原格子属性修正（RefreshCellProperties → 基础值）
    ///   2) 还原 OnApplyActions（可逆动作）
    ///   3) 取消被动效果订阅
    ///   4) 执行 OnExpireActions
    ///   5) 触发移除事件
    /// </summary>
    public void RemoveEnvironment(Cell cell)
    {
        if (cell == null || cell.Environment == null || cell.Environment.IsExpired) return;
        var env = cell.Environment;
        env.IsExpired = true;

        // ── 还原 OnApplyActions（可逆动作，如 ModifyCellStatAction 的 MoveCost）──────────
        // 注意顺序：先 Revert（此时环境字段修正仍生效，MoveCost = 基础+字段+动作增量），
        // 再解除挂载刷新（回到基础值）——若先刷新，Revert 会在基础值上重复扣减。
        if (env.Data.OnApplyActions != null && env.Data.OnApplyActions.Length > 0)
        {
            var ctx = new Context { TargetCell = cell, TargetUnit = cell.OccupyingUnit, SourceUnit = env.SourceUnit };
            foreach (var action in env.Data.OnApplyActions)
                action.Revert(ctx);
        }

        // ── 解除挂载 + 还原属性修正（先置 null 再刷新，RefreshCellProperties 读到的是无环境状态） ──
        cell.Environment = null;
        _activeEnvironments.Remove(cell);
        RefreshCellProperties(cell);

        // ── 取消被动效果订阅 ──────────────────────────────────────
        string tag = $"env_{env.Data.EnvironmentID}_{cell.GridPos}";
        EventBus.Instance?.UnsubscribeByTag(tag);

        // ── 执行到期动作 ──────────────────────────────────────────
        if (env.Data.OnExpireActions != null && env.Data.OnExpireActions.Length > 0)
        {
            var ctx = new Context { TargetCell = cell, TargetUnit = cell.OccupyingUnit, SourceUnit = env.SourceUnit };
            foreach (var action in env.Data.OnExpireActions)
                action.Execute(ctx);
        }

        // ── 触发事件 ──────────────────────────────────────────────
        EventBus.Instance?.Fire(EventType.OnEnvironmentRemoved,
            new Context { TargetCell = cell, SourceUnit = env.SourceUnit, SourceTeam = env.SourceUnit?.Team ?? Team.Neutral });

        // ── 通知 View 层擦除环境图块（事件驱动）────────────────────
        EnvironmentRemoved?.Invoke(cell, env);

        GD.Print($"[EnvironmentManager] 移除: {env.Data.EnvironmentName} @ {cell.GridPos}");
    }

    /// <summary>
    /// 驱散：按 EnvironmentID 查找并移除指定格子上的该环境。
    /// </summary>
    public void RemoveEnvironmentByData(Cell cell, string environmentID)
    {
        if (cell == null || cell.Environment == null) return;
        if (cell.Environment.Data.EnvironmentID != environmentID) return;
        RemoveEnvironment(cell);
    }

    // ======================================================================
    // 回合倒计时
    // ======================================================================

    /// <summary>
    /// 每回合结束时调用（BattleManager.OnEnterRoundEnd）：
    ///   1) 所有环境的 RemainingTurns-1（Duration&gt;0 时）
    ///   2) 执行 OnRoundEndActions
    ///   3) 归零/0 持续的环境调用 RemoveEnvironment（含 OnExpireActions）
    /// </summary>
    public void TickAllEnvironments()
    {
        var toRemove = new List<Cell>();

        foreach (var env in _activeEnvironments.Values)
        {
            if (env.IsExpired) continue;

            bool expired = false;

            // Duration = 0: 当回合移除，不倒计时
            if (env.Data.Duration == 0)
            {
                expired = true;
            }
            // Duration > 0: 正常倒计时，最小减到 0
            else if (env.Data.Duration > 0)
            {
                if (env.RemainingTurns > 0)
                    env.RemainingTurns--;
                if (env.RemainingTurns <= 0)
                    expired = true;
            }
            // Duration < 0 (-1): 永久，跳过

            // 执行回合结束动作（即使是归零的这回合也执行）
            if (env.Data.OnRoundEndActions != null && env.Data.OnRoundEndActions.Length > 0)
            {
                var ctx = new Context { TargetCell = env.Cell, TargetUnit = env.Cell?.OccupyingUnit, SourceUnit = env.SourceUnit };
                foreach (var action in env.Data.OnRoundEndActions)
                    action.Execute(ctx);
            }

            if (expired)
                toRemove.Add(env.Cell);
        }

        foreach (var cell in toRemove)
            RemoveEnvironment(cell);
    }

    // ======================================================================
    // 格子属性统一重算
    // ======================================================================

    /// <summary>
    /// 统一重算格子的运行时属性：基础地形值 + 环境修正；单位占据时 CanStand/CanPass 强制 false。
    /// 环境施加/移除与 UnitManager 释放格子时调用——保证环境修正不被占位逻辑覆盖。
    /// 注意：调用方须先置 OccupyingUnit=null（释放场景），占据状态由本方法内部处理。
    /// </summary>
    public void RefreshCellProperties(Cell cell)
    {
        if (cell == null) return;

        // 基础值
        cell.MoveCost = cell.BaseBlock?.MoveCost ?? 1;
        cell.CanStand = cell.BaseBlock?.CanStand ?? true;
        cell.CanPass = cell.BaseBlock?.CanPass ?? true;

        // 环境修正
        var env = cell.Environment;
        if (env?.Data != null)
        {
            cell.MoveCost = System.Math.Max(0, cell.MoveCost + env.Data.MoveCostDelta);

            switch (env.Data.CanStandOverride)
            {
                case CellPropertyOverride.ForceTrue: cell.CanStand = true; break;
                case CellPropertyOverride.ForceFalse: cell.CanStand = false; break;
            }
            switch (env.Data.CanPassOverride)
            {
                case CellPropertyOverride.ForceTrue: cell.CanPass = true; break;
                case CellPropertyOverride.ForceFalse: cell.CanPass = false; break;
            }
        }

        // 占位优先：单位占据时不可站立/不可穿越（覆盖环境修正与基础值）
        if (cell.OccupyingUnit != null)
        {
            cell.CanStand = false;
            cell.CanPass = false;
        }
    }

    // ======================================================================
    // 查询
    // ======================================================================

    /// <summary>获取格子上的环境（无则 null）</summary>
    public Environment GetEnvironment(Cell cell)
    {
        return cell?.Environment;
    }

    /// <summary>检查格子上是否有指定 ID 的环境</summary>
    public bool HasEnvironment(Cell cell, string environmentID)
    {
        return cell?.Environment?.Data.EnvironmentID == environmentID;
    }
}
