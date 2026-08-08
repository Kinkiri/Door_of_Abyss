using Godot;
using System;

/// <summary>
/// 暂停菜单（战斗场景）：Esc 或左上角暂停按钮（PauseButton）切换暂停。
/// 真实暂停（GetTree().Paused，EnemyAI 计时器等全部停止）+ BGM 降音量（DuckBgm）。
/// 面板含继续游戏 / 重新开始 / 设置 / 标题画面 / 退出游戏；设置复用 SettingsPanel 组件。
/// 渐入渐出动画（Backdrop 暗幕渐显 + 面板下滑弹出，同主界面/设置面板同款）；
/// 经 PanelStack 统一管理 Esc：暂停入栈，Esc 关闭栈顶（设置面板先关，再按恢复游戏）。
/// 节点 ProcessMode.Always：暂停时本层仍响应输入与动画（CreateTween 跟随本节点同样不受暂停影响）。
/// </summary>
[GlobalClass]
public partial class PauseMenu : Control, IPanel
{
    /// <summary>设置面板组件（挂在本节点下的 SettingsPanel.tscn 实例）</summary>
    [Export] private SettingsPanel SettingsPanelUI;

    public static PauseMenu Instance { get; private set; }

    // IPanel（PanelStack 成员：暂停中入栈，Esc 关闭栈顶即恢复游戏）
    public bool IsOpen => _paused;
    public void Open() => SetPaused(true);
    public void Close() => SetPaused(false);

    private bool _paused;
    private bool _animating;
    private ColorRect _backdrop;
    private PanelContainer _panel;
    private Vector2 _basePos;

    public override void _Ready()
    {
        Instance = this;
        PanelStack.Clear();   // 场景入口：丢弃上一场景残留的面板（避免 Esc 访问已释放节点）
        ProcessMode = ProcessModeEnum.Always;   // 暂停时本节点仍处理输入与动画

        _backdrop = GetNode<ColorRect>("Backdrop");
        _panel = GetNode<PanelContainer>("Panel");
        _basePos = _panel.Position;

        GetNode<Button>("Panel/Margin/Root/Buttons/ResumeButton").Pressed += () => SetPaused(false);
        GetNode<Button>("Panel/Margin/Root/Buttons/RestartButton").Pressed += RestartLevel;
        GetNode<Button>("Panel/Margin/Root/Buttons/SettingsButton").Pressed += () =>
        {
            AudioManager.Instance?.PlayUiSfx("ui_click");
            SettingsPanelUI?.Show();
        };
        // 战斗中改 AI 难度（游戏页下拉）→ 解暂停并重载本关，让新难度立即生效
        if (SettingsPanelUI != null)
            SettingsPanelUI.AiDifficultyChanged += RestartLevel;
        GetNode<Button>("Panel/Margin/Root/Buttons/TitleButton").Pressed += BackToTitle;
        GetNode<Button>("Panel/Margin/Root/Buttons/QuitButton").Pressed += () =>
        {
            SetPaused(false, animate: false);   // 恢复 BGM 音量（残留 30% 会带到下一场景）
            GetTree().Quit();
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            // 面板栈统一处理：关闭栈顶（如暂停中的设置面板），栈空则打开暂停
            if (!PanelStack.HandleEscape())
                SetPaused(true);
        }
    }

    /// <summary>
    /// 切换暂停：树暂停（真实暂停）+ 整层显隐 + BGM 降音量 + 渐入渐出动画。
    /// animate=false 为同步路径（切场景/退出前立即恢复暂停状态，避免新场景继承暂停）。
    /// </summary>
    public void SetPaused(bool paused, bool animate = true)
    {
        if (_paused == paused || _animating) return;
        _paused = paused;
        AudioManager.Instance?.PlayUiSfx("ui_click");

        if (paused)
        {
            PanelStack.Push(this);
            GetTree().Paused = true;
            AudioManager.Instance?.DuckBgm(true);
            SettingsPanelUI?.Hide();   // 防上次暂停残留
            ((CanvasLayer)GetParent()).Visible = true;
            _animating = true;
            AnimatePanelIn(() => _animating = false);
        }
        else if (animate)
        {
            PanelStack.Pop(this);
            _animating = true;
            AnimatePanelOut(() =>
            {
                ((CanvasLayer)GetParent()).Visible = false;
                _animating = false;
                GetTree().Paused = false;
                AudioManager.Instance?.DuckBgm(false);
            });
        }
        else
        {
            PanelStack.Pop(this);
            ((CanvasLayer)GetParent()).Visible = false;
            GetTree().Paused = false;
            AudioManager.Instance?.DuckBgm(false);
        }
    }

    /// <summary>重新开始：恢复暂停状态（同步）→ 重载当前关卡（LevelSelection.Selected 仍指向本关）</summary>
    private void RestartLevel()
    {
        SetPaused(false, animate: false);
        GetTree().ChangeSceneToFile("res://Scenes/Game/Level.tscn");
    }

    /// <summary>返回标题画面：先恢复暂停状态（BGM 音量/树暂停），否则新场景继承暂停且 BGM 残留降音量</summary>
    private void BackToTitle()
    {
        SetPaused(false, animate: false);
        GetTree().ChangeSceneToFile("res://Scenes/Game/title.tscn");
    }

    // ======================================================================
    // 面板动画（与主界面/设置面板同款）
    // ======================================================================

    private void AnimatePanelIn(Action onDone)
    {
        _backdrop.Visible = true;
        _panel.Visible = true;
        _panel.Position = _basePos + new Vector2(0, 60);
        _backdrop.Modulate = new Color(1, 1, 1, 0);
        _panel.Modulate = new Color(1, 1, 1, 0);

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_backdrop, "modulate:a", 1f, 0.25f);
        tween.TweenProperty(_panel, "modulate:a", 1f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_panel, "position:y", _basePos.Y, 0.35f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenCallback(Callable.From(() => onDone?.Invoke()));
    }

    private void AnimatePanelOut(Action onDone)
    {
        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_backdrop, "modulate:a", 0f, 0.25f);
        tween.TweenProperty(_panel, "modulate:a", 0f, 0.25f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(_panel, "position:y", _basePos.Y + 60f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            _panel.Visible = false;
            _backdrop.Visible = false;
            _panel.Position = _basePos;
            onDone?.Invoke();
        }));
    }
}
