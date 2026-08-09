/// <summary>
/// 跨场景资源引脚（规避 Godot C# 引擎 bug #83762：自定义 C# Resource 的托管包装器被 GC 回收、
/// 而原生资源仍存活于 ResourceCache，之后再次被引用会触发 "gchandle.is_released()" 崩溃）。
/// CardLibrary / UnitLibrary / LevelLibrary 已钉住卡牌/单位/关卡模板；
/// 此处补齐场景直接加载、不经静态库的零散资源（PlayerData 等）。
/// </summary>
public static class ResourcePins
{
    /// <summary>玩家全局数据（Level.tscn 加载，含门列表/玩家卡组）</summary>
    public static PlayerData PlayerData;

    /// <summary>当前关卡数据（编辑器直跑时走 Level.tscn 固定引用，不经 LevelLibrary，同样需要钉住）</summary>
    public static LevelData LevelData;
}
