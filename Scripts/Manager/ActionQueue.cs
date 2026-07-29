using Godot;
using System.Collections.Generic;

/// <summary>
/// 动作序列器，按顺序逐个执行动作，每个动作之间等待 AnimationDuration 秒。
/// 支持 EnqueueFront 插队（反击/连击），队列空时回调 onComplete。
/// 后续由 View 层订阅 ActionStarted 信号播放对应动画，完成后调 Next()。
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
        foreach (var a in actions)
        {
            if (a == null) continue;
            _queue.Enqueue(new QueuedAction { Action = a, Context = CloneContext(ctx) });
        }

        if (onComplete.Method != null)
        {
            // 队列尾部标记一个空动作，携带回调
            _queue.Enqueue(new QueuedAction { Action = null, Context = null, OnComplete = onComplete });
        }

        if (!_isProcessing) ProcessNext();
    }

    /// <summary>插队到队头（反击/连击/被动触发）</summary>
    public void EnqueueFront(GameAction action, Context ctx)
    {
        if (action == null) return;
        var temp = new List<QueuedAction> { new QueuedAction { Action = action, Context = CloneContext(ctx) } };
        temp.AddRange(_queue);
        _queue.Clear();
        foreach (var q in temp) _queue.Enqueue(q);

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
            if (item.OnComplete.Method != null)
                item.OnComplete.CallDeferred();
            ProcessNext();
            return;
        }

        GD.Print($"[ActionQueue] 执行: {item.Action.GetType().Name} (动画 {item.Action.AnimationDuration}s)");

        // 执行动作逻辑
        item.Action.Execute(item.Context);

        // 发送信号供 View 层播动画（后续由 View 订阅）
        EmitSignal(SignalName.ActionStarted, item.Action.GetType().Name, item.Action.AnimationDuration);

        // 等待动画时长后执行下一个
        var timer = GetTree().CreateTimer(item.Action.AnimationDuration);
        timer.Timeout += ProcessNext;
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

    [Signal] public delegate void ActionStartedEventHandler(string actionName, float duration);

    private struct QueuedAction
    {
        public GameAction Action;
        public Context Context;
        public Callable OnComplete;
    }
}
