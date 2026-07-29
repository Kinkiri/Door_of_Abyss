using Godot;

/// <summary>
/// 初始化管理器，确保所有 Manager 的 Instance 就绪后再按依赖顺序 Init。
/// 需添加到场景或 AutoLoad 中，执行顺序优先于其他 Manager。
/// </summary>
public partial class InitManager : Node
{
    public override void _Ready()
    {
        // 等一帧让所有 Manager 的 _Ready（Instance = this）跑完
        CallDeferred(nameof(InitAll));
    }

    private void InitAll()
    {
        GD.Print("[InitManager] 开始初始化所有管理器");

        // 无依赖
        MapManager.Instance?.Init();
        UnitManager.Instance?.Init();
        CardManager.Instance?.Init();
        EventBus.Instance?.Init();
        EnemyAI.Instance?.Init();
        SelectionManager.Instance?.Init();
        BuffManager.Instance?.Init();

        // 依赖 SelectionManager 的事件订阅
        BattleManager.Instance?.Init();

        GD.Print("[InitManager] 所有管理器初始化完成");
    }
}
