using Godot;

/// <summary>
/// Buff 运行时实例，追踪每个活跃 Buff 的剩余回合、叠层和来源。
/// 纯 C# class，不继承 Godot 类型。
/// </summary>
public partial class Buff
{
    /// <summary>Buff 模板数据引用</summary>
    public BuffData Data { get; set; }

    /// <summary>剩余回合数</summary>
    public int RemainingTurns { get; set; }

    /// <summary>当前叠加层数</summary>
    public int StackCount { get; set; }

    /// <summary>最近一次行动开始时的叠层快照。WearMode 减层只消耗快照内的旧层——行动中新增的层不受本次行动磨损</summary>
    public int StacksAtActionStart { get; set; }

    /// <summary>施加者（谁上的这个 buff）</summary>
    public Unit SourceUnit { get; set; }

    /// <summary>Buff 是否已过期/移除</summary>
    public bool IsExpired { get; set; }

    public Buff() { }

    public Buff(BuffData data, Unit sourceUnit)
    {
        Data = data;
        RemainingTurns = data.Duration;
        StackCount = 1;
        SourceUnit = sourceUnit;
        IsExpired = false;
    }

    public override string ToString()
    {
        return $"[Buff {Data?.BuffID}] {Data?.BuffName} | 剩余 {RemainingTurns} 回合 叠层 {StackCount}";
    }
}
