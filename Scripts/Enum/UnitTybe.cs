using Godot;

/// <summary>
/// 单位类型枚举，定义单位的基础行为分类
/// </summary>
public enum UnitType
{
    /// <summary>兵种：主动移动，主动攻击，玩家可操作</summary>
    Squad,

    /// <summary>建筑：不可移动，被动攻击（如箭塔、炮台）</summary>
    Building,

    /// <summary>障碍物：纯挡路，不可移动、不可攻击、不可通过</summary>
    Obstacle,

    /// <summary>召唤物：自主移动，自主攻击，玩家不可操作</summary>
    Summon,

    /// <summary>特殊：特殊需求时使用，具体行为由关联逻辑定义</summary>
    Special,

    /// <summary>门（水晶）：各阵营核心，归零则战败</summary>
    Door,
}

