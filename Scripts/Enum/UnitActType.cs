/// <summary>
/// 单位行动类型。BattleManager 触发 OnUnitAct 事件时写入 Context.ActType，
/// 供被动效果区分移动/攻击（如义肢"移动不消耗"）。
/// </summary>
public enum UnitActType
{
    None,
    Move,
    Attack,
}
