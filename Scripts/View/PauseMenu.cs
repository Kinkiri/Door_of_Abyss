using Godot;

/// <summary>
/// 暂停菜单（战斗场景）：Esc 切换暂停。
/// 真实暂停（GetTree().Paused，EnemyAI 计时器等全部停止）+ BGM 降音量（DuckBgm）。
/// 面板含继续游戏 / 设置 / 标题画面 / 退出游戏；设置复用 SettingsPanel 组件。
/// 节点 ProcessMode.Always：暂停时本层仍响应输入与动画。
/// </summary>
[GlobalClass]
public partial class PauseMenu : Control
{
    /// <summary>设置面板组件（挂在本节点下的 SettingsPanel.tscn 实例）</summary>
    [Export] private SettingsPanel SettingsPanelUI;

    private bool _paused;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // 暂停时本节点仍处理输入

        GetNode<Button>("Panel/Margin/Root/Buttons/ResumeButton").Pressed += () => SetPaused(false);
        GetNode<Button>("Panel/Margin/Root/Buttons/SettingsButton").Pressed += () =>
        {
            AudioManager.Instance?.PlayUiSfx("ui_click");
            SettingsPanelUI?.Show();
        };
        GetNode<Button>("Panel/Margin/Root/Buttons/TitleButton").Pressed += BackToTitle;
        GetNode<Button>("Panel/Margin/Root/Buttons/QuitButton").Pressed += () =>
        {
            SetPaused(false);   // 恢复 BGM 音量（残留 30% 会带到下一场景）
            GetTree().Quit();
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            SetPaused(!_paused);
        }
    }

    /// <summary>切换暂停：树暂停（真实暂停）+ 整层显隐（含设置面板实例）+ BGM 降音量</summary>
    public void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;

        GetTree().Paused = paused;
        ((CanvasLayer)GetParent()).Visible = paused;   // PauseLayer 整体显隐（PauseMenu + SettingsPanelUI）
        AudioManager.Instance?.DuckBgm(paused);
        AudioManager.Instance?.PlayUiSfx("ui_click");
    }

    /// <summary>返回标题画面：先恢复暂停状态（BGM 音量/树暂停），否则新场景继承暂停且 BGM 残留降音量</summary>
    private void BackToTitle()
    {
        SetPaused(false);
        GetTree().ChangeSceneToFile("res://Scenes/Game/title.tscn");
    }
}
