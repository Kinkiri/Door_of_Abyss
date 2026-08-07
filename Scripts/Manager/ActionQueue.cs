using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 动作序列器，按顺序逐个执行动作，每个动作之间等待 AnimationDuration 秒。
/// 队列空时回调 onComplete。后续由 View 层订阅 ActionStarted 信号播放对应动画，完成后调 Next()。
/// </summary>
public partial class ActionQueue : Node
{
    public static ActionQueue Instance { get; private set; }

    private Queue<QueuedAction> _queue = new();
    private bool _isProcessing = false;

    public override void _Ready()
    {
        Instance = this;
    }

    public void Init() { }

    /// <summary>排到队尾（批量动作）</summary>
    public void Enqueue(GameAction[] actions, Context ctx, Callable onComplete = default)
    {
        PopulateBattleData(ctx);

        foreach (var a in actions)
        {
            if (a == null) continue;
            _queue.Enqueue(new QueuedAction { Action = a, Context = CloneContext(ctx) });
        }

        // Godot 4.7 中 Callable.Method 是 StringName（方法名），委托封装的 Callable（Callable.From）
        // 构造时 _method 为 null 而 _delegate 非空——两种封装都要识别，否则攻击等 lambda 回调会静默丢失
        if (onComplete.Method != null || onComplete.Delegate != null)
        {
            // 队列尾部标记一个空动作，携带回调
            _queue.Enqueue(new QueuedAction { Action = null, Context = null, OnComplete = onComplete });
        }

        if (!_isProcessing) ProcessNext();
    }

    /// <summary>清空队列（如阶段切换时）</summary>
    public void Clear()
    {
        _queue.Clear();
        _isProcessing = false;
    }

    private void ProcessNext()
    {
        if (_queue.Count == 0)
        {
            _isProcessing = false;
            return;
        }

        _isProcessing = true;
        var item = _queue.Dequeue();

        // 空动作只携带回调（队列末尾标记）
        if (item.Action == null)
        {
            // 同 Enqueue：委托封装（Callable.From）的 Method 为 null，需同时检查 Delegate
            if (item.OnComplete.Method != null || item.OnComplete.Delegate != null)
            {
                // 暂停加固：CallDeferred 不受 GetTree().Paused 影响，若回调恰在暂停帧执行
                // 会推进核心逻辑（如 CheckVictory/选中单位）；包装一层暂停检查跳过，
                // 队列本身已冻结，继续游戏后由阶段流程自然衔接
                Callable guarded = Callable.From(() =>
                {
                    if (GetTree().Paused) return;
                    item.OnComplete.Call();
                });
                guarded.CallDeferred();
            }
            ProcessNext();
            return;
        }

        GD.Print($"[ActionQueue] 执行: {item.Action.GetType().Name} (动画 {item.Action.AnimationDuration}s)");

        // 执行动作逻辑
        item.Action.Execute(item.Context);

        // 通知 ViewAnimator 等订阅者播放视觉效果（C# 事件，无类型限制）
        OnActionExecuted?.Invoke(item.Action, item.Context);

        // 等待动画时长后执行下一个
        // processAlways:false —— 树暂停（Esc 暂停）时动作节奏停止，队列冻结
        var timer = GetTree().CreateTimer(item.Action.AnimationDuration, processAlways: false);
        timer.Timeout += ProcessNext;
    }

    /// <summary>
    /// 填充上下文的战场数据（Map/ActiveUnits），供 TargetResolver 纯函数使用。
    /// 未填充时从 Manager 单例读取，调用方显式传入时优先保留。
    /// </summary>
    private static void PopulateBattleData(Context ctx)
    {
        if (ctx == null) return;
        ctx.Map ??= MapManager.Instance?.Map;
        ctx.ActiveUnits ??= UnitManager.Instance?.ActiveUnits;
    }

    /// <summary>
    /// 浅拷贝 Context（值类型字段拷贝，引用类型字段共享）。
    /// 避免队列中多个 QueuedAction 持同一 Context 引用导致后续覆盖。
    /// </summary>
    private static Context CloneContext(Context src)
    {
        if (src == null) return null;
        return new Context
        {
            Map = src.Map,
            ActiveUnits = src.ActiveUnits,
            SourceUnit = src.SourceUnit,
            TargetUnit = src.TargetUnit,
            TargetUnits = src.TargetUnits,
            SourceTeam = src.SourceTeam,
            TargetTeam = src.TargetTeam,
            SourceCard = src.SourceCard,
            SourceCell = src.SourceCell,
            TargetCell = src.TargetCell,
            TargetCells = src.TargetCells,
        };
    }

    // ========================================================================
    // 事件（C# 事件，非 Godot 信号 — 无参数类型限制）
    // ViewAnimator 订阅此事件播放视觉效果
    // ========================================================================
    /// <summary>每个动作执行完毕后触发，参数为 (已执行的动作, 上下文)</summary>
    public static event Action<GameAction, Context> OnActionExecuted;

    private struct QueuedAction
    {
        public GameAction Action;
        public Context Context;
        public Callable OnComplete;
    }
}
