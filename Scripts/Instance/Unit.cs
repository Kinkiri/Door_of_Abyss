using Godot;
using System;

/// <summary>
/// 运行时单位，表示战场上的一个单位，包含战斗中的可变状态
/// </summary>
public partial class Unit
{
    /// <summary>
    /// 单位实例的唯一标识符，由 UnitManager 在创建时分配
    /// </summary>
    public int ID { get; set; }

    /// <summary>单位基础数据（引用自 UnitData 资源）</summary>
    public UnitData UnitData { get; set; }

    /// <summary>当前所在格子坐标</summary>
    public Vector2I GridPos { get; set; }

    /// <summary>是否可以被选中（例如在战斗中，某些单位可能被禁用或隐藏，无法选择）</summary>
    public bool CanSelect { get; set; } = true;

    /// <summary>所属阵营</summary>
    public Team Team { get; set; } = Team.Neutral;

    /// <summary>从 UnitData 拷贝过来的运行时属性</summary>
    public int AttackPower { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    /// <summary>体力（移动范围半径，曼哈顿距离；单值，无上限/剩余之分）</summary>
    public int Stamina { get; set; }
    public int AttackDistance { get; set; }

    /// <summary>攻击形状（透传模板 UnitData.AttackShape；null = 默认菱形）。变身换模板自动更新</summary>
    public CellShape AttackShape => UnitData?.AttackShape;
    /// <summary>当前行动点数（行动消耗，每回合开始恢复满）</summary>
    public int ActionPoints { get; set; }
    /// <summary>行动点上限（可被 Buff/装备修改，最小不低于 1）</summary>
    public int MaxActionPoints { get; set; }

    /// <summary>本回合行动次数（移动/攻击各算一次；出牌和被动自动攻击不计）。RoundStart 归零</summary>
    public int ActionsThisTurn { get; set; }

    /// <summary>上回合是否行动过（主动移动/攻击，ActionsThisTurn 快照；强制位移不计）。RoundStart 更新，门经济据此判定</summary>
    public bool LastTurnActed { get; set; }
    public UnitType Type { get; set; }


    /// <summary>是否存活</summary>
    public bool IsAlive => CurrentHP > 0;

    /// <summary>用于强制死亡，通常在战斗结束或单位被移除时使用</summary>
    public bool IsDead { get; set; }

    /// <summary>是否可被攻击（false 时免疫所有攻击）</summary>
    public bool CanBeAttacked { get; set; } = true;
    public Unit() { }

    public event Action OnUnitUpdate;

    public Unit(UnitData unitData, Vector2I gridPos, Team team)
    {
        UnitData = unitData;
        GridPos = gridPos;
        Team = team;
        InitializeFromData();
    }

    /// <summary>
    /// 将 UnitData 的静态值拷贝到运行时字段
    /// </summary>
    public void InitializeFromData()
    {
        AttackPower = UnitData?.AttackPower ?? 1;
        MaxHP = UnitData?.HealthPoints ?? 2;
        CurrentHP = MaxHP;
        Stamina = UnitData?.Stamina ?? 1;
        AttackDistance = UnitData?.AttackDistance ?? 1;
        MaxActionPoints = System.Math.Max(1, UnitData?.ActionPoints ?? 1);
        ActionPoints = MaxActionPoints;
        Type = UnitData?.Type ?? UnitType.兵种;
    }

    /// <summary>
    /// 获取单位的描述信息
    /// </summary>
    public string Description =>
        $"名字: {UnitData?.UnitName ?? "未知单位"}\n" +
        $"ID: {UnitData?.UnitID ?? "UnknownUnit"}\n" +
        $"HP: {CurrentHP}/{MaxHP}\n" +
        $"攻击力: {AttackPower}\n" +
        $"体力: {Stamina}\n" +
        $"攻击范围: {AttackDistance}\n" +
        $"类型: {Type}\n" +
        $"动作点数: {ActionPoints}/{MaxActionPoints}";


    public override string ToString()
    {
        return $"[Unit {UnitData?.UnitID}] {UnitData?.UnitName} | " +
               $"HP={CurrentHP}/{MaxHP} 体力={Stamina} 类型={Type} 动作点数={ActionPoints}/{MaxActionPoints}";
    }

    /// <summary>
    /// 通知 UI 层刷新显示（触发 OnUnitUpdate 事件）。
    /// 属性变更后（HP、位置等）必须调用此方法，否则 UnitView 不会更新。
    /// </summary>
    public void UpdateUnit()
    {
        OnUnitUpdate?.Invoke();
    }
}
