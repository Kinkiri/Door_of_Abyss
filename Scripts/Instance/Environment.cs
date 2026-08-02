/// <summary>
/// 环境运行时实例，追踪每个活跃环境的剩余回合、所在格子和来源。
/// 纯 C# class，不继承 Godot 类型。
/// </summary>
public partial class Environment
{
    /// <summary>环境模板数据引用</summary>
    public EnvironmentData Data { get; set; }

    /// <summary>所在格子</summary>
    public Cell Cell { get; set; }

    /// <summary>施加者（谁放的这个环境）</summary>
    public Unit SourceUnit { get; set; }

    /// <summary>剩余回合数</summary>
    public int RemainingTurns { get; set; }

    /// <summary>是否已过期/移除</summary>
    public bool IsExpired { get; set; }

    public Environment() { }

    public Environment(EnvironmentData data, Cell cell, Unit sourceUnit)
    {
        Data = data;
        Cell = cell;
        SourceUnit = sourceUnit;
        RemainingTurns = data.Duration;
        IsExpired = false;
    }

    public override string ToString()
    {
        return $"[Environment {Data?.EnvironmentID}] {Data?.EnvironmentName} | 剩余 {RemainingTurns} 回合 @ {Cell?.GridPos}";
    }
}
