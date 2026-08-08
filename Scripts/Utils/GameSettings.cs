using Godot;

/// <summary>
/// 玩家全局设置读写（user://settings.cfg）。
/// 分段管理：audio 段归 AudioManager、video 段归 SettingsPanel、game 段（AI 难度等）归本类。
/// </summary>
public static class GameSettings
{
    /// <summary>设置存档路径（与 AudioManager/SettingsPanel 共用同一文件，各段 Load 后 Save 防覆盖）</summary>
    public const string SettingsCfgPath = "user://settings.cfg";

    /// <summary>AI 难度"跟随关卡配置"时的存档值</summary>
    public const string AiFollowLevel = "跟随关卡";

    /// <summary>
    /// 读取玩家 AI 难度覆盖；返回 null = 跟随关卡配置（LevelData.AiLevel）。
    /// 存档值：简单/标准/狡诈（AiLevel.ToString），或 AiFollowLevel。
    /// </summary>
    public static AiLevel? GetAiLevelOverride()
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsCfgPath);
        string value = (string)cfg.GetValue("game", "ai_level", AiFollowLevel);
        switch (value)
        {
            case "简单": return AiLevel.简单;
            case "标准": return AiLevel.标准;
            case "狡诈": return AiLevel.狡诈;
            default: return null;
        }
    }

    /// <summary>保存玩家 AI 难度覆盖（null = 跟随关卡）</summary>
    public static void SaveAiLevelOverride(AiLevel? level)
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsCfgPath);
        cfg.SetValue("game", "ai_level", level?.ToString() ?? AiFollowLevel);
        cfg.Save(SettingsCfgPath);
    }
}
