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
    [Export] public Team Team { get; set; } = Team.Neutral;

    /// <summary>从 UnitData 拷贝过来的运行时属性</summary>
    public int AttackPower { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int RemainingStamina { get; set; }
    public int MaxStamina { get; set; }
    public int AttackDistance { get; set; }
    public int ActionPoints { get; set; }   
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
        MaxStamina = UnitData?.Stamina ?? 1;
        RemainingStamina = MaxStamina;
        AttackDistance = UnitData?.AttackDistance ?? 1;
        ActionPoints = UnitData?.ActionPoints ?? 1;
        Type = UnitData?.Type ?? UnitType.Squad;
    }

    /// <summary>
    /// 获取单位的描述信息
    /// </summary>
    public string Description =>
        $"名字: {UnitData?.UnitName ?? "未知单位"}\n" +
        $"ID: {UnitData?.UnitID ?? "UnknownUnit"}\n" +
        $"HP: {CurrentHP}/{MaxHP}\n" +
        $"攻击力: {AttackPower}\n" +
        $"体力: {RemainingStamina}/{MaxStamina}\n" +
        $"攻击范围: {AttackDistance}\n" +
        $"类型: {Type}\n" +
        $"动作点数: {ActionPoints}";


    public override string ToString()
    {
        return $"[Unit {UnitData?.UnitID}] {UnitData?.UnitName} | " +
               $"HP={CurrentHP}/{MaxHP} 体力={RemainingStamina}/{MaxStamina} 类型={Type} 动作点数={ActionPoints}";
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
