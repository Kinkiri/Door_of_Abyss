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
            else if (fileName.EndsWith(".tres.remap", StringComparison.OrdinalIgnoreCase))
            {
                // 打包导出（安卓/iOS）后 .tres 被 UID 重映射为 .tres.remap；
                // 逻辑路径仍是 .tres（Godot VFS 自动重映射），去掉 .remap 后缀加入
                paths.Add(fullPath.Substring(0, fullPath.Length - ".remap".Length));
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
            // 用非泛型 Load + as 检查：类型不匹配的文件（误放入目录的无关 Resource）跳过并警告，而不是抛异常
            var res = ResourceLoader.Load(path) as DataType;
            if (res != null)
            {
                resources.Add(res);
                GD.Print($"  ✓ {path.GetFile()}");
            }
            else
            {
                GD.PushWarning($"  ✗ 跳过（非 {typeof(DataType).Name} 或加载失败）: {path}");
            }
        }

        return resources;
    }
}
