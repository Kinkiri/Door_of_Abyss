using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 资源库类，提供资源加载和管理的基础功能
/// </summary>
public partial class Library
{
    /// <summary>
    /// 递归获取指定文件夹下所有 .tres 文件的完整路径列表（含子文件夹）
    /// </summary>
    protected static List<string> GetAllTresPaths(string folderPath)
    {
        var paths = new List<string>();

        using var dir = DirAccess.Open(folderPath);
        if (dir == null)
        {
            GD.PushError($"无法打开文件夹: {folderPath}");
            return paths;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.StartsWith("."))  // 跳过 . 和 ..
            {
                fileName = dir.GetNext();
                continue;
            }

            string fullPath = folderPath.PathJoin(fileName);

            if (dir.CurrentIsDir())
            {
                // 递归子文件夹
                paths.AddRange(GetAllTresPaths(fullPath));
            }
            else if (fileName.GetExtension().ToLower() == "tres")
            {
                paths.Add(fullPath);
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
        return paths;
    }
    /// <summary>
    /// 根据路径列表加载资源，并返回资源列表
    /// </summary>
    /// <typeparam name="DataType">数据类型</typeparam>
    /// <param name="paths">资源路径列表</param>
    /// <returns>资源列表</returns>
    protected static List<DataType> LoadResourcesFromPaths<DataType>
        (List<string> paths) where DataType : Resource
    {
        var resources = new List<DataType>();

        GD.Print($"加载 {paths.Count} 个 {typeof(DataType).Name}...");

        foreach (string path in paths)
        {
            var res = ResourceLoader.Load<DataType>(path);
            if (res != null)
            {
                resources.Add(res);
                GD.Print($"  ✓ {path.GetFile()}");
            }
            else
            {
                GD.PushWarning($"  ✗ 加载失败: {path}");
            }
        }

        return resources;
    }
}
