/// <summary>目标筛选结果集类型</summary>
public enum TargetKind
{
    /// <summary>结果为单位集合（Shape 生成/过滤的单位）</summary>
    Unit,

    /// <summary>结果为格子集合（Shape 生成的格子，供召唤/位移等动作使用）</summary>
    Cell,
}
