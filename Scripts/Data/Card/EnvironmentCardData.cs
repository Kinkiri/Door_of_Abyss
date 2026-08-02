using Godot;

/// <summary>
/// 环境卡牌数据类，继承自 CardData。
/// 打出后在目标格子放置环境（TargetFilters 形状须为 SingleCell，Kind=Cell）。
/// </summary>
[GlobalClass]
public partial class EnvironmentCardData : CardData
{
    [Export] public EnvironmentData EnvironmentData { get; set; }
}
