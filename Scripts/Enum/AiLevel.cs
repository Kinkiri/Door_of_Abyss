/// <summary>敌人 AI 等级（关卡级配置，决定决策深度）</summary>
public enum AiLevel
{
    /// <summary>简单：基础行为（攻击最近 + 直线逼近）</summary>
    简单,
    /// <summary>标准：目标打分 + 移动进射程走位（一轮内移动+攻击）</summary>
    标准,
    /// <summary>狡诈：标准 + 威胁规避绕路 + 刷怪格回避/主动让位</summary>
    狡诈,
}
