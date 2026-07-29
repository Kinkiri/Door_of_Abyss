using Godot;
using System.Collections.Generic;

/// <summary>
/// 关卡库，加载所有关卡数据并提供按名称查找。
/// 后续用于选关界面。
/// </summary>
public partial class LevelLibrary : Library
{
    private const string LevelDataPath = "res://Resource/Data/Levels/";

    /// <summary>按顺序排列的关卡列表</summary>
    public static List<LevelData> LevelList { get; private set; } = new();

    /// <summary>按关卡名称查找</summary>
    public static Dictionary<string, LevelData> LevelDictionary { get; private set; } = new();

    static LevelLibrary()
    {
        LevelList.AddRange(LoadResourcesFromPaths<LevelData>(GetAllTresPaths(LevelDataPath)));

        foreach (var level in LevelList)
        {
            if (!LevelDictionary.ContainsKey(level.LevelName))
            {
                LevelDictionary.Add(level.LevelName, level);
            }
            else
            {
                GD.PrintErr($"关卡名称重复: {level.LevelName}");
            }
        }

        GD.Print($"已加载 {LevelList.Count} 个关卡数据:");
        foreach (var level in LevelList)
        {
            GD.Print($"  {level.LevelName}: {level.Description}");
        }
    }

    public static LevelData GetLevelByName(string name)
    {
        if (LevelDictionary.TryGetValue(name, out var level))
            return level;
        GD.PrintErr($"未找到关卡: {name}");
        return null;
    }
}
