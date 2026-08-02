using Godot;

/// <summary>
/// 环境模板数据：覆盖在基础地形之上的地图图层（"格子的 Buff"）。
/// 定义环境对格子属性的修正、持续回合、施加/到期动作与被动效果。
/// 一个格子同时最多一个环境；施加新环境时旧环境完整还原后替换（替换式覆盖）。
/// </summary>
[GlobalClass]
public partial class EnvironmentData : Resource
{
    /// <summary>唯一标识</summary>
    [Export] public string EnvironmentID { get; set; } = "UnknownEnvironment";

    /// <summary>显示名称</summary>
    [Export] public string EnvironmentName { get; set; } = "未命名环境";

    /// <summary>描述</summary>
    [Export] public string Description { get; set; } = "暂无描述";

    /// <summary>环境图标（图集未配置时的备选展示）</summary>
    [Export] public Texture2D Icon { get; set; }

    /// <summary>
    /// 持续回合数。
    /// 0 = 当回合 RoundEnd 移除（不倒计时）；
    /// N (&gt;0) = 持续 N 回合（当前回合计入）；
    /// -1 = 永久持续（不回合计时）。
    /// </summary>
    [Export] public int Duration { get; set; } = -1;

    /// <summary>移动消耗修正（正=更难走，负=更好走）。环境移除时自动还原</summary>
    [Export] public int MoveCostDelta { get; set; } = 0;

    /// <summary>可站立覆盖（Unchanged=不改，沿用基础地形；单位占据时仍强制不可站立）</summary>
    [Export] public CellPropertyOverride CanStandOverride { get; set; } = CellPropertyOverride.Unchanged;

    /// <summary>可穿越覆盖（Unchanged=不改，沿用基础地形；单位占据时仍强制不可穿越）</summary>
    [Export] public CellPropertyOverride CanPassOverride { get; set; } = CellPropertyOverride.Unchanged;

    /// <summary>环境图层（EnvironmentViewManager 渲染用）图集源 ID</summary>
    [Export] public int AtlasSourceId { get; set; } = 0;

    /// <summary>环境图层（EnvironmentViewManager 渲染用）图集坐标</summary>
    [Export] public Vector2I AtlasCoords { get; set; } = Vector2I.Zero;

    /// <summary>
    /// 施加时执行的动作序列（ctx.TargetCell=环境格子，ctx.TargetUnit=格子上单位）。
    /// 可逆动作（ModifyCellStatAction 的 MoveCost 等）在环境移除时自动还原。
    /// </summary>
    [Export] public GameAction[] OnApplyActions { get; set; }

    /// <summary>到期/移除时执行的动作序列（一次性效果）</summary>
    [Export] public GameAction[] OnExpireActions { get; set; }

    /// <summary>每回合结束时执行的动作序列（在倒计时/移除判断之前执行）</summary>
    [Export] public GameAction[] OnRoundEndActions { get; set; }

    /// <summary>持续期间生效的被动效果（复用 EffectData 系统，订阅 owner=Environment）</summary>
    [Export] public EffectData[] PassiveEffects { get; set; }

    public override string ToString()
    {
        return $"[Environment: {EnvironmentID}] {EnvironmentName} | 持续 {Duration} 回合";
    }
}
