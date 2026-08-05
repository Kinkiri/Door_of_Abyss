using Godot;
using System;
using System.Text;

[GlobalClass]
public partial class MainMenu : Control
{
    [Export] private TextureRect _title;
    [Export] private TextureRect _background;
    [Export] private GpuParticles2D _dust;
    [Export] private VBoxContainer _menu;
    [Export] private ColorRect _fadeOut;
    [Export] private Label _version;
    [Export] private Label _toast;
    [Export] private ColorRect _creditsBackdrop;
    [Export] private PanelContainer _creditsPanel;
    [Export] private ColorRect _levelSelectBackdrop;
    [Export] private PanelContainer _levelSelectPanel;

    [Export] private float _dustSpeed = 120f;
    [Export] private float _dustMinX = 120f;
    [Export] private float _dustMaxX = 1800f;

    private bool _switching;
    private bool _creditsAnimating;
    private bool _levelSelectAnimating;
    private Vector2 _creditsBasePos;
    private Vector2 _levelSelectBasePos;
    private int _dustDir = 1;

    private LevelData _selectedLevel;
    private Button _selectedLevelButton;
    private Label _detailName;
    private Label _detailDesc;
    private Label _detailInfo;

    public override void _Ready()
    {
        _fadeOut.MouseFilter = MouseFilterEnum.Ignore;
        _toast.Modulate = Colors.Transparent;
        _title.PivotOffset = _title.Texture.GetSize() / 2f;
        _creditsBasePos = _creditsPanel.Position;
        _levelSelectBasePos = _levelSelectPanel.Position;
        SetupMenu();
        SetupCredits();
        SetupLevelSelect();
        BuildLevelList();
        PlayEntrance();
        StartBreathAnimation();
        StartBackgroundBreath();
    }

    public override void _Process(double delta)
    {
        // 粒子在屏幕底部区域左右匀速往返移动
        Vector2 pos = _dust.Position;
        pos.X += _dustDir * _dustSpeed * (float)delta;
        if (pos.X >= _dustMaxX)
        {
            pos.X = _dustMaxX;
            _dustDir = -1;
        }
        else if (pos.X <= _dustMinX)
        {
            pos.X = _dustMinX;
            _dustDir = 1;
        }
        _dust.Position = pos;
    }

    private void SetupMenu()
    {
        foreach (Node child in _menu.GetChildren())
        {
            if (child is not HBoxContainer row || row.GetChildCount() < 2) continue;
            ColorRect indicator = row.GetChild<ColorRect>(0);
            Button button = row.GetChild<Button>(1);
            indicator.Modulate = new Color(1, 1, 1, 0);
            button.Pressed += () => OnMenuPressed(button);
            button.MouseEntered += () => SetIndicator(indicator, true);
            button.MouseExited += () => SetIndicator(indicator, false);
        }
    }

    private void SetIndicator(ColorRect indicator, bool hover)
    {
        Tween tween = CreateTween();
        tween.TweenProperty(indicator, "modulate:a", hover ? 1f : 0f, 0.18f);
    }

    private void SetupCredits()
    {
        _creditsPanel.GetNode<Button>("Margin/Root/Header/CloseButton").Pressed += HideCredits;
        _creditsBackdrop.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                HideCredits();
        };
    }

    private void SetupLevelSelect()
    {
        _detailName = _levelSelectPanel.GetNode<Label>("Margin/Root/Body/Right/DetailScroll/DetailContent/DetailName");
        _detailDesc = _levelSelectPanel.GetNode<Label>("Margin/Root/Body/Right/DetailScroll/DetailContent/DetailDesc");
        _detailInfo = _levelSelectPanel.GetNode<Label>("Margin/Root/Body/Right/DetailScroll/DetailContent/DetailInfo");
        _levelSelectPanel.GetNode<Button>("Margin/Root/Header/CloseButton").Pressed += HideLevelSelect;
        _levelSelectPanel.GetNode<Button>("Margin/Root/Body/Right/BottomBar/EnterButton").Pressed += StartSelectedLevel;
        _levelSelectBackdrop.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                HideLevelSelect();
        };
    }

    private void BuildLevelList()
    {
        var list = _levelSelectPanel.GetNode<VBoxContainer>("Margin/Root/Body/ListPanel/Scroll/LevelList");
        foreach (var level in LevelLibrary.LevelList)
        {
            var btn = new Button
            {
                Text = level.LevelName,
                Flat = true,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left,
            };
            btn.AddThemeFontSizeOverride("font_size", 26);
            btn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            var captured = level;
            btn.Pressed += () => SelectLevel(captured, btn);
            list.AddChild(btn);
        }

        if (LevelLibrary.LevelList.Count > 0 && list.GetChildCount() > 0)
            SelectLevel(LevelLibrary.LevelList[0], (Button)list.GetChild(0));
    }

    private void SelectLevel(LevelData level, Button btn)
    {
        if (_selectedLevelButton != null)
            _selectedLevelButton.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
        _selectedLevelButton = btn;
        _selectedLevelButton.AddThemeColorOverride("font_color", Colors.White);
        _selectedLevel = level;
        UpdateLevelDetail(level);
    }

    private void UpdateLevelDetail(LevelData level)
    {
        _detailName.Text = level.LevelName;
        _detailDesc.Text = level.Description;

        var sb = new StringBuilder();
        sb.Append($"波次: {level.Waves?.Length ?? 0}");
        if (level.Waves != null)
        {
            foreach (var wave in level.Waves)
                sb.Append($"　第 {wave.Round} 回合: {Summarize(wave.UnitDatas, u => u.UnitName)}\n");
        }
        sb.Append("　卡组: ");
        if (level.LevelDeck != null)
            sb.Append($"{Summarize(level.LevelDeck.Cards, c => c.CardName)}（{level.LevelDeck.DeckName}）\n");
        else
            sb.Append("玩家卡组");
        _detailInfo.Text = sb.ToString();
    }

    /// <summary>把数组按名称统计为 "名称×数量" 紧凑列表，保持出现顺序；数量 1 不加后缀</summary>
    private string Summarize<T>(T[] items, Func<T, string> getName)
    {
        if (items == null || items.Length == 0) return "(无)";
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        var order = new System.Collections.Generic.List<string>();
        foreach (var item in items)
        {
            string name = getName(item);
            if (name.Length == 0) name = "?";
            if (!counts.ContainsKey(name))
            {
                counts[name] = 0;
                order.Add(name);
            }
            counts[name]++;
        }
        var parts = new string[order.Count];
        for (int i = 0; i < order.Count; i++)
            parts[i] = counts[order[i]] > 1 ? $"{order[i]}×{counts[order[i]]}" : order[i];
        return string.Join("、", parts);
    }

    private void OnMenuPressed(Button button)
    {
        if (button.Text == "开始游戏") ShowLevelSelect();
        else if (button.Text == "关于") ShowCredits();
        else if (button.Text == "退出游戏") StartExit();
        else ShowToast("功能开发中，敬请期待");
    }

    private void StartExit()
    {
        if (_switching) return;
        _switching = true;
        Tween tween = CreateTween();
        tween.TweenProperty(_fadeOut, "color:a", 1f, 0.5f);
        tween.TweenCallback(Callable.From(() => GetTree().Quit()));
    }

    private void StartSelectedLevel()
    {
        if (_selectedLevel == null)
        {
            ShowToast("请先选择一个关卡");
            return;
        }
        if (_switching) return;
        _switching = true;
        LevelSelection.Selected = _selectedLevel;
        Tween tween = CreateTween();
        tween.TweenProperty(_fadeOut, "color:a", 1f, 0.55f);
        tween.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile("res://Scenes/Game/Level.tscn")));
    }

    private void ShowCredits()
    {
        if (_creditsAnimating || _creditsPanel.Visible) return;
        _creditsAnimating = true;
        AnimatePanelIn(_creditsPanel, _creditsBackdrop, _creditsBasePos,
            () => _creditsAnimating = false);
    }

    private void HideCredits()
    {
        if (_creditsAnimating || !_creditsPanel.Visible) return;
        _creditsAnimating = true;
        AnimatePanelOut(_creditsPanel, _creditsBackdrop, _creditsBasePos,
            () => _creditsAnimating = false);
    }

    private void ShowLevelSelect()
    {
        if (_levelSelectAnimating || _levelSelectPanel.Visible) return;
        _levelSelectAnimating = true;
        AnimatePanelIn(_levelSelectPanel, _levelSelectBackdrop, _levelSelectBasePos,
            () => _levelSelectAnimating = false);
    }

    private void HideLevelSelect()
    {
        if (_levelSelectAnimating || !_levelSelectPanel.Visible) return;
        _levelSelectAnimating = true;
        AnimatePanelOut(_levelSelectPanel, _levelSelectBackdrop, _levelSelectBasePos,
            () => _levelSelectAnimating = false);
    }

    private void AnimatePanelIn(Control panel, ColorRect backdrop, Vector2 basePos, Action onDone)
    {
        backdrop.Visible = true;
        panel.Visible = true;
        panel.Position = basePos + new Vector2(0, 60);
        backdrop.Modulate = new Color(1, 1, 1, 0);
        panel.Modulate = new Color(1, 1, 1, 0);

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(backdrop, "modulate:a", 1f, 0.25f);
        tween.TweenProperty(panel, "modulate:a", 1f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(panel, "position:y", basePos.Y, 0.35f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenCallback(Callable.From(() => onDone?.Invoke()));
    }

    private void AnimatePanelOut(Control panel, ColorRect backdrop, Vector2 basePos, Action onDone)
    {
        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(backdrop, "modulate:a", 0f, 0.25f);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.25f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(panel, "position:y", basePos.Y + 60f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            panel.Visible = false;
            backdrop.Visible = false;
            panel.Position = basePos;
            onDone?.Invoke();
        }));
    }

    private void ShowToast(string text)
    {
        _toast.Text = text;
        Tween tween = CreateTween();
        tween.TweenProperty(_toast, "modulate:a", 1f, 0.25f);
        tween.TweenInterval(1.2f);
        tween.TweenProperty(_toast, "modulate:a", 0f, 0.6f);
    }

    private void PlayEntrance()
    {
        _background.Modulate = new Color(_background.Modulate, 0f);
//        _dust.Modulate = new Color(_dust.Modulate, 0f);
        _title.Modulate = new Color(_title.Modulate, 0f);
        _version.Modulate = new Color(_version.Modulate, 0f);

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_background, "modulate:a", 1f, 1.8f);
        tween.TweenProperty(_title, "modulate:a", 1f, 2.2f).SetDelay(0.1f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        //tween.TweenProperty(_dust, "modulate:a", 1f, 2.8f).SetDelay(0.7f);
        tween.TweenProperty(_version, "modulate:a", 0.35f, 1.8f).SetDelay(0.9f);

        int i = 0;
        foreach (Node child in _menu.GetChildren())
        {
            if (child is not Control row) continue;
            row.Modulate = new Color(row.Modulate, 0f);
            row.Scale = new Vector2(0.94f, 0.94f);
            float delay = 0.55f + i * 0.18f;
            tween.TweenProperty(row, "modulate:a", 1f, 1.0f).SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(row, "scale", Vector2.One, 1.0f).SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            i++;
        }
    }

    private void StartBreathAnimation()
    {
        Vector2 baseScale = _title.Scale;
        Tween scale = CreateTween();
        scale.SetLoops();
        scale.SetTrans(Tween.TransitionType.Sine);
        scale.SetEase(Tween.EaseType.InOut);
        scale.TweenProperty(_title, "scale", baseScale * 1.05f, 3.2f);
        scale.TweenProperty(_title, "scale", baseScale, 3.2f);

        Tween light = CreateTween();
        light.SetLoops();
        light.SetTrans(Tween.TransitionType.Sine);
        light.SetEase(Tween.EaseType.InOut);
        light.TweenProperty(_title, "modulate:a", 0.86f, 3.2f).SetDelay(1.5f);
        light.TweenProperty(_title, "modulate:a", 1f, 3.2f);
    }

    private void StartBackgroundBreath()
    {
        _background.PivotOffset = _background.Size / 2f;
        Tween tween = CreateTween();
        tween.SetLoops();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_background, "scale", Vector2.One * 1.05f, 8f);
        tween.TweenProperty(_background, "scale", Vector2.One, 8f);
    }
}
