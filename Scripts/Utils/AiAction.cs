using Godot;

/// <summary>AI 决策结果动作类型</summary>
public enum AiActionKind
{
    Attack, // 攻击
    Move,   // 移动
    Skip,   // 无有价值行动：保留行动点，结束该单位本回合
}

/// <summary>
/// AI 单次决策结果（决策与执行分离：AiTactics 纯逻辑产出，EnemyAI 负责预告镜头并执行）。
/// 决策与执行之间状态可能变化（前一单位已行动），执行前按需重新校验。
/// </summary>
public class AiAction
{
    public AiActionKind Kind;

    /// <summary>Kind=Attack：攻击目标</summary>
    public Unit Target;

    /// <summary>Kind=Move：落点</summary>
    public Vector2I? MovePos;

    /// <summary>总效用分（日志/测试断言）</summary>
    public int Utility;

    /// <summary>攻击价值分量：Kind=Attack 时=ScoreTarget；Kind=Move 时=该落点的移动后攻击价值（0=无攻击价值，AP&lt;2 恒 0）</summary>
    public int AttackValue;

    /// <summary>决策理由（中文，日志）</summary>
    public string Reason;
}
