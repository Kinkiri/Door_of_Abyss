using Godot;

/// <summary>
/// Buff 模板数据，定义增益/减益的效果、持续时间和叠加规则。
/// 编辑器可配置的 Resource。
/// </summary>
[GlobalClass]
public partial class BuffData : Resource
{
    /// <summary>唯一标识，用于叠层判断</summary>
    [Export] public string BuffID { get; set; } = "UnknownBuff";

    /// <summary>显示名称</summary>
    [Export] public string BuffName { get; set; } = "未命名 Buff";

    /// <summary>描述</summary>
    [Export] public string Description { get; set; } = "暂无描述";

    /// <summary>
    /// 持续回合数。
    /// 0 = 直接移除（当回合 RoundEnd 移除，不倒计时）；
    /// N (&gt;0) = 持续 N 回合（当前回合计入）；
    /// -1 = 永久持续（不回合计时，不会到期移除）。
    /// </summary>
    [Export] public int Duration { get; set; } = 1;

    /// <summary>
    /// 最大叠加层数。
    /// 0 = 直接移除（编辑器不填 0）；
    /// 1 = 不叠加（同类 Buff 直接刷新）；
    /// N (&gt;0) = 最多 N 层；
    /// -1 = 无限叠加。
    /// </summary>
    [Export] public int MaxStack { get; set; } = 1;


    /// <summary>
    /// 是否允许移除（如被净化、驱散等）。默认 true = 可移除，false = 不可移除。
    /// </summary>
    [Export] public bool CanBeChanged { get; set; } = true;

    /// <summary>固定效果：OnApplyActions 只执行一次，效果与层数解耦（有层即生效，归零/移除才还原）。
    /// 层数仅作计数器（如"义肢"的耐久/磨损）。默认 false = 层数倍率（每层执行一次）</summary>
    [Export] public bool FixedEffect { get; set; }

    /// <summary>
    /// 施加时执行的动作序列。
    /// 属性修改型 GameAction（ModifyAttackPower 等）会在到期时自动还原。
    /// 一次性效果（Heal 等）不可逆，到期不还原。
    /// </summary>
    [Export] public GameAction[] OnApplyActions { get; set; }

    /// <summary>
    /// 到期时执行的动作序列（如爆炸、治疗等一次性效果）。
    /// 注意：属性修改的还原由系统自动处理，不需要放到这里。
    /// </summary>
    [Export] public GameAction[] OnExpireActions { get; set; }

    /// <summary>
    /// 每回合结束时执行的动作序列（在 RemainingTurns-1 之后、判断归零之前执行）。
    /// </summary>
    [Export] public GameAction[] OnRoundEndActions { get; set; }

    /// <summary>持续期间生效的被动效果（复用 EffectData 系统）</summary>
    [Export] public EffectData[] PassiveEffects { get; set; }

    /// <summary>Buff 图标</summary>
    [Export] public Texture2D Icon { get; set; }

    public override string ToString()
    {
        return $"[Buff: {BuffID}] {BuffName} | 持续 {Duration} 回合 最大叠层 {MaxStack}";
    }
}
