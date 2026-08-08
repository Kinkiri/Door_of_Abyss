using Godot;
using System;

/// <summary>
/// 设置面板组件（主界面与战斗暂停菜单共用）。
/// 根节点为全屏 Control（子节点：Backdrop 暗幕 + Panel 面板），自管显示动画与设置逻辑：
/// 左侧选项卡（音量/画面/游戏）+ 右侧内容页；audio 段持久化由 AudioManager 负责，
/// video 段与 game 段（AI 难度）归本组件（路径常量见 GameSettings）。
/// 调用方只需 Show() / Hide()。
/// </summary>
[GlobalClass]
public partial class SettingsPanel : Control, IPanel
{
    /// <summary>AI 难度变更（游戏页下拉选择时触发；战斗中 PauseMenu 订阅后重启关卡）</summary>
    public event Action AiDifficultyChanged;

    private static readonly (string text, Vector2I size)[] Resolutions =
    {
        ("1280×720", new Vector2I(1280, 720)),
        ("1600×900", new Vector2I(1600, 900)),
        ("1920×1080", new Vector2I(1920, 1080)),
        ("2560×1440", new Vector2I(2560, 1440)),
    };

    private ColorRect _backdrop;
    private PanelContainer _panel;
    private Vector2 _basePos;
    private bool _animating;

    public bool IsVisiblePanel => _panel?.Visible ?? false;

    // IPanel（PanelStack 成员）
    public bool IsOpen => IsVisiblePanel;
    public void Open() => Show();
    public void Close() => Hide();

    public override void _Ready()
    {
        _backdrop = GetNode<ColorRect>("Backdrop");
        _panel = GetNode<PanelContainer>("Panel");
        _basePos = _panel.Position;

        _panel.GetNode<Button>("Margin/Root/Header/CloseButton").Pressed += Hide;
        _backdrop.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                Hide();
        };

        SetupAudioPage();
        SetupVideoPage();
        SetupGamePage();
        SetupSettingsTabs();
    }

    /// <summary>打开设置面板（暗幕渐显 + 面板下滑弹出）。new：区别于 CanvasItem.Show()（无动画）</summary>
    public new void Show()
    {
        if (_animating || (_panel?.Visible ?? true)) return;
        PanelStack.Push(this);
        AudioManager.Instance?.PlayUiSfx("ui_click");
        _animating = true;
        AnimatePanelIn(() => _animating = false);
    }

    /// <summary>关闭设置面板。new：区别于 CanvasItem.Hide()（无动画）</summary>
    public new void Hide()
    {
        if (_animating || !(_panel?.Visible ?? false)) return;
        PanelStack.Pop(this);
        AudioManager.Instance?.PlayUiSfx("ui_click");
        _animating = true;
        AnimatePanelOut(() => _animating = false);
    }

    // ======================================================================
    // 面板动画（与主界面关于/选关面板同款）
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

    // ======================================================================
    // 音量页
    // ======================================================================

    private void SetupAudioPage()
    {
        var audio = AudioManager.Instance;
        if (audio == null) return;

        SetupVolumeRow("MusicRow", audio.GetMusicVolume(), audio.SetMusicVolume);
        SetupVolumeRow("SfxRow", audio.GetSfxVolume(), audio.SetSfxVolume);
        SetupVolumeRow("UiRow", audio.GetUiVolume(), audio.SetUiVolume);
    }

    /// <summary>配置一行音量滑块：Slider(0~100) ↔ 百分比 Label，ValueChanged → 应用音量</summary>
    private void SetupVolumeRow(string rowName, float initialVolume, Action<float> applyVolume)
    {
        var row = _panel.GetNode<HBoxContainer>($"Margin/Root/Body/Content/AudioPage/{rowName}");
        var slider = row.GetNode<HSlider>("Slider");
        var valueLabel = row.GetNode<Label>("ValueLabel");

        // SetValueNoSignal：初始化不触发 ValueChanged（避免加载时重复写盘）
        slider.SetValueNoSignal(initialVolume * 100);
        valueLabel.Text = $"{Mathf.RoundToInt(initialVolume * 100)}%";

        slider.ValueChanged += (double v) =>
        {
            applyVolume((float)(v / 100.0));
            valueLabel.Text = $"{Mathf.RoundToInt(v)}%";
        };
    }

    // ======================================================================
    // 画面页
    // ======================================================================

    private void SetupVideoPage()
    {
        var resOption = _panel.GetNode<OptionButton>("Margin/Root/Body/Content/VideoPage/ResolutionRow/Option");
        var modeOption = _panel.GetNode<OptionButton>("Margin/Root/Body/Content/VideoPage/ModeRow/Option");

        foreach (var r in Resolutions)
            resOption.AddItem(r.text);
        modeOption.AddItem("窗口");
        modeOption.AddItem("全屏");

        // 读取已存设置（video 段）
        var cfg = new ConfigFile();
        cfg.Load(GameSettings.SettingsCfgPath);
        bool fullscreen = (bool)cfg.GetValue("video", "fullscreen", false);
        string resolutionText = (string)cfg.GetValue("video", "resolution", "1920×1080");

        int resIdx = 0;
        for (int i = 0; i < Resolutions.Length; i++)
            if (Resolutions[i].text == resolutionText) { resIdx = i; break; }
        resOption.Select(resIdx);
        modeOption.Select(fullscreen ? 1 : 0);

        // 应用存档设置（窗口模式 → 分辨率，顺序保证切回窗口时恢复大小）
        ApplyWindowMode(fullscreen);
        ApplyResolution(Resolutions[resIdx].size);

        resOption.ItemSelected += (long idx) =>
        {
            ApplyResolution(Resolutions[(int)idx].size);
            SaveVideoSettings(resOption, modeOption);
        };
        modeOption.ItemSelected += (long idx) =>
        {
            bool fs = idx == 1;
            ApplyWindowMode(fs);
            if (!fs) ApplyResolution(Resolutions[resOption.Selected].size);   // 切回窗口恢复大小
            SaveVideoSettings(resOption, modeOption);
        };
    }

    /// <summary>保存画面设置（先 Load 保留 audio 段，防覆盖）</summary>
    private static void SaveVideoSettings(OptionButton resOption, OptionButton modeOption)
    {
        var cfg = new ConfigFile();
        cfg.Load(GameSettings.SettingsCfgPath);
        cfg.SetValue("video", "fullscreen", modeOption.Selected == 1);
        cfg.SetValue("video", "resolution", Resolutions[resOption.Selected].text);
        cfg.Save(GameSettings.SettingsCfgPath);
    }

    private static void ApplyResolution(Vector2I size)
    {
        // 仅窗口模式生效；全屏下窗口大小由系统决定
        if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed)
            DisplayServer.WindowSetSize(size);
    }

    private static void ApplyWindowMode(bool fullscreen)
    {
        DisplayServer.WindowSetMode(fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    // ======================================================================
    // 游戏页
    // ======================================================================

    /// <summary>配置游戏页：敌方 AI 难度下拉（跟随关卡/简单/标准/狡诈），变更即存盘并触发 AiDifficultyChanged</summary>
    private void SetupGamePage()
    {
        var aiOption = _panel.GetNode<OptionButton>("Margin/Root/Body/Content/GamePage/AiRow/Option");
        aiOption.AddItem(GameSettings.AiFollowLevel);
        aiOption.AddItem("简单");
        aiOption.AddItem("标准");
        aiOption.AddItem("狡诈");

        // 读取已存覆盖值：null=跟随关卡（第 0 项），否则 简单/标准/狡诈 对应 1/2/3
        AiLevel? current = GameSettings.GetAiLevelOverride();
        aiOption.Select(current.HasValue ? (int)current.Value + 1 : 0);

        aiOption.ItemSelected += (long idx) =>
        {
            AiLevel? level = idx == 0 ? null : (AiLevel?)(int)(idx - 1);
            if (level == current) return;   // 重选当前项不触发（防误重启关卡）
            current = level;
            GameSettings.SaveAiLevelOverride(level);
            AiDifficultyChanged?.Invoke();
        };
    }

    // ======================================================================
    // 左侧选项卡
    // ======================================================================

    private void SetupSettingsTabs()
    {
        var tabVolume = _panel.GetNode<Button>("Margin/Root/Body/Tabs/TabVolume");
        var tabVideo = _panel.GetNode<Button>("Margin/Root/Body/Tabs/TabVideo");
        var tabGame = _panel.GetNode<Button>("Margin/Root/Body/Tabs/TabGame");
        tabVolume.Pressed += () => ShowSettingsTab("AudioPage", tabVolume);
        tabVideo.Pressed += () => ShowSettingsTab("VideoPage", tabVideo);
        tabGame.Pressed += () => ShowSettingsTab("GamePage", tabGame);
        ShowSettingsTab("AudioPage", tabVolume);   // 默认音量页
    }

    private void ShowSettingsTab(string pageName, Button activeTab)
    {
        var pages = _panel.GetNode<VBoxContainer>("Margin/Root/Body/Content");
        pages.GetNode<VBoxContainer>("AudioPage").Visible = pageName == "AudioPage";
        pages.GetNode<VBoxContainer>("VideoPage").Visible = pageName == "VideoPage";
        pages.GetNode<VBoxContainer>("GamePage").Visible = pageName == "GamePage";

        var tabVolume = _panel.GetNode<Button>("Margin/Root/Body/Tabs/TabVolume");
        var tabVideo = _panel.GetNode<Button>("Margin/Root/Body/Tabs/TabVideo");
        var tabGame = _panel.GetNode<Button>("Margin/Root/Body/Tabs/TabGame");
        SetTabHighlight(tabVolume, activeTab == tabVolume);
        SetTabHighlight(tabVideo, activeTab == tabVideo);
        SetTabHighlight(tabGame, activeTab == tabGame);
    }

    private static void SetTabHighlight(Button tab, bool active)
    {
        tab.AddThemeColorOverride("font_color",
            active ? new Color(1, 1, 1, 1) : new Color(1, 1, 1, 0.7f));
    }
}
