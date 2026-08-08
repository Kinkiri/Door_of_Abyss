using Godot;

/// <summary>
/// 对局内提示数据：指定回合触发显示一条消息提示。
/// 由 LevelData.Hints 数组引用，策划在 .tres 中配置。
/// </summary>
[GlobalClass]
public partial class HintData : Resource
{
    /// <summary>触发回合（0 = 放门/游戏开始阶段，N = 第 N 回合 RoundStart）</summary>
    [Export] public int TriggerRound { get; set; } = 1;

    /// <summary>提示消息内容</summary>
    [Export] public string Message { get; set; } = "";

    /// <summary>是否自动缩回（false = 常驻，直到玩家按 ✕ 关闭）</summary>
    [Export] public bool AutoRetract { get; set; } = true;

    /// <summary>停留时长（秒），AutoRetract=true 时到点自动缩回</summary>
    [Export] public float HoverDuration { get; set; } = 3f;
}
