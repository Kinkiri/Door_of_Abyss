using Godot;

/// <summary>
/// 单位类型枚举，定义单位的基础行为分类
/// 注意：枚举下标已固化到 .tres 资源（门=5 等），新增/调整只能追加，不可改动顺序
/// </summary>
public enum UnitType
{
    /// <summary>兵种：主动移动，主动攻击，玩家可操作</summary>
    兵种,

    /// <summary>建筑：不可移动，被动攻击（如箭塔、炮台）</summary>
    建筑,

    /// <summary>障碍物：纯挡路，不可移动、不可攻击、不可通过</summary>
    障碍物,

    /// <summary>召唤物：自主移动，自主攻击，玩家不可操作</summary>
    召唤物,

    /// <summary>特殊物：特殊需求时使用，具体行为由关联逻辑定义</summary>
    特殊物,

    /// <summary>门（水晶）：各阵营核心，归零则战败</summary>
    门,
}
