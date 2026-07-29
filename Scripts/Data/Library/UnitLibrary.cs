using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 单位库类，负责加载和管理所有单位数据资源
/// </summary>
public partial class UnitLibrary : Library
{
    /// <summary>单位数据资源路径，所有单位数据资源文件应放置在此路径下</summary>
    const string UnitDataPath = "res://Resource/Data/Units/";

    /// <summary>单位列表，包含所有已注册的单位对象</summary>
    public static List<UnitData> UnitList { get; private set; } = new();

    /// <summary>单位字典，键为单位ID，值为单位对象</summary>
    public static Dictionary<string, UnitData> UnitDictionary { get; private set; } = new();

    static UnitLibrary()
    {
        // 读取所有UnitData资源文件
        UnitList.AddRange(LoadResourcesFromPaths<UnitData>(GetAllTresPaths(UnitDataPath)));

        // 初始化单位字典
        foreach (var unit in UnitList)
        {
            if (!UnitDictionary.ContainsKey(unit.UnitID))
            {
                UnitDictionary.Add(unit.UnitID, unit);
            }
            else
            {
                GD.PrintErr($"单位ID重复: {unit.UnitID}");
            }
        }

        // 输出已加载的单位信息
        GD.Print($"已加载 {UnitList.Count} 个单位数据:");
        foreach (var unit in UnitList)
        {
            GD.Print($"  {unit.ToString()}");
        }
    }

    public static UnitData GetUnitByID(string unitID)
    {
        if (UnitDictionary.TryGetValue(unitID, out var unit))
        {
            return unit;
        }
        else
        {
            GD.PrintErr($"未找到单位ID: {unitID}");
            return null;
        }
    }
}
