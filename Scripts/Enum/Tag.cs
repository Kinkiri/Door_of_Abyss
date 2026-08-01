/// <summary>普通标签</summary>
public enum Tag
{
    科技,
    宗教,
    /// <summary>
    /// 使义肢额外增加生命值上限
    /// </summary>
    生命义肢,
    /// <summary>
    /// 使义肢额外增加攻击力
    /// </summary>
    攻击义肢,
    /// <summary>
    /// 使义肢额外增加体力
    /// </summary>
    体力义肢,
    /// <summary>
    /// 使义肢额外增加行动点上限
    /// </summary>
    行动义肢,
    /// <summary>
    /// 使义肢额外增加攻击范围
    /// </summary>
    距离义肢,
    动物,
    /// <summary>
    /// 使义肢在移动时不被消耗（只有攻击才磨损）
    /// </summary>
    耐用义肢,
    /// <summary>
    /// 使义肢在攻击时不被消耗（只有移动才磨损）
    /// </summary>
    耐打义肢
}
