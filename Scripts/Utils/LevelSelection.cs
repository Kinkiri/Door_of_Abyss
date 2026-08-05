/// <summary>
/// 选关结果传递：主界面选择关卡 → 战斗场景加载。
/// BattleManager 在 _Ready 时读取，覆盖 Level.tscn 中的固定引用。
/// </summary>
public static class LevelSelection
{
    public static LevelData Selected;
}
