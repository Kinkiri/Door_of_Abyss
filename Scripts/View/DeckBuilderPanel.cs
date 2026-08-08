using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 配置卡组面板（主界面选关界面打开）。
/// 根节点为全屏 Control（子节点：Backdrop 暗幕 + Panel 面板），自管显示动画与选卡逻辑：
/// 左：卡牌库滚动列表（懒加载 CardLibrary），点击行选中并在右侧详情区查看完整卡牌信息；
///     每行 卡名/费用/数量 + [-] [+]；单卡最多 3 张、卡组最多 30 张，超出按钮置灰；
/// 右：详情区（名称/类型/稀有度/费用/描述/目标/标签 + 单位·环境·装备附加信息）；
/// 变更即实时写入 user://deck.cfg（PlayerDeckSave），关闭面板即完成配置。
/// 调用方只需 Show() / Hide()。
/// </summary>
[GlobalClass]
public partial class DeckBuilderPanel : Control, IPanel
{
    /// <summary>单卡数量上限</summary>
    private const int MaxPerCard = 3;
    /// <summary>卡组总张数上限</summary>
    private const int MaxDeckSize = 30;

    private ColorRect _backdrop;
    private PanelContainer _panel;
    private Label _statusLabel;
    private Label _deckSummary;
    private Label _detailName;
    private Label _detailDesc;
    private Label _detailInfo;
    private Vector2 _basePos;
    private bool _animating;
    private bool _listBuilt;
    /// <summary>当前选中卡牌的 CardID（null = 未选中）</summary>
    private string _selectedId;

    /// <summary>当前配置：CardID → 数量</summary>
    private readonly Dictionary<string, int> _counts = new();
    /// <summary>行控件：CardID → (名称Label, 数量Label, [-]按钮, [+]按钮)</summary>
    private readonly Dictionary<string, (Label nameLabel, Label countLabel, Button minus, Button plus)> _rows = new();

    public bool IsVisiblePanel => _panel?.Visible ?? false;

    // IPanel（PanelStack 成员）
    public bool IsOpen => IsVisiblePanel;
    public void Open() => Show();
    public void Close() => Hide();

    public override void _Ready()
    {
        _backdrop = GetNode<ColorRect>("Backdrop");
        _panel = GetNode<PanelContainer>("Panel");
        ApplyPanelClamp();
        _basePos = _panel.Position;
        _statusLabel = GetNode<Label>("Panel/Margin/Root/StatusBar/StatusLabel");
        _deckSummary = GetNode<Label>("Panel/Margin/Root/DeckSummary");
        _detailName = GetNode<Label>("Panel/Margin/Root/Body/DetailPanel/Scroll/DetailContent/DetailName");
        _detailDesc = GetNode<Label>("Panel/Margin/Root/Body/DetailPanel/Scroll/DetailContent/DetailDesc");
        _detailInfo = GetNode<Label>("Panel/Margin/Root/Body/DetailPanel/Scroll/DetailContent/DetailInfo");

        _panel.GetNode<Button>("Margin/Root/Header/CloseButton").Pressed += Hide;
        _backdrop.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                Hide();
        };
    }

    /// <summary>打开配置卡组面板（懒加载：首次打开才访问 CardLibrary 构建列表）。new：区别于 CanvasItem.Show()（无动画）</summary>
    public new void Show()
    {
        if (_animating || (_panel?.Visible ?? true)) return;
        PanelStack.Push(this);
        AudioManager.Instance?.PlayUiSfx("ui_click");
        ApplyPanelClamp();
        if (!_listBuilt) BuildList();
        LoadFromSave();
        // 选中卡失效（如存档变更）时回退默认选中第一张
        if (_selectedId == null || !CardLibrary.CardDictionary.ContainsKey(_selectedId))
        {
            if (CardLibrary.CardList.Count > 0)
                SelectCard(CardLibrary.CardList[0].CardID);
        }
        RefreshAll();
        _animating = true;
        AnimatePanelIn(() => _animating = false);
    }

    /// <summary>关闭配置卡组面板（配置已实时保存，无需确认）。new：区别于 CanvasItem.Hide()（无动画）</summary>
    public new void Hide()
    {
        if (_animating || !(_panel?.Visible ?? false)) return;
        PanelStack.Pop(this);
        AudioManager.Instance?.PlayUiSfx("ui_click");
        _animating = true;
        AnimatePanelOut(() => _animating = false);
    }

    // ======================================================================
    // 面板自适应
    // ======================================================================

    /// <summary>
    /// 面板尺寸钳制到视口内（保持居中对称，Position 不变）：小窗口/低分辨率下面板不致超屏。
    /// 编辑器里拖大的尺寸会被自动收缩；1920×1080 等大视口不受影响。
    /// </summary>
    private void ApplyPanelClamp()
    {
        Vector2 viewport = GetViewportRect().Size;
        float maxW = viewport.X - 80f;
        float maxH = viewport.Y - 80f;
        float w = _panel.OffsetRight - _panel.OffsetLeft;
        float h = _panel.OffsetBottom - _panel.OffsetTop;
        if (w > maxW)
        {
            float half = maxW / 2f;
            _panel.OffsetLeft = -half;
            _panel.OffsetRight = half;
        }
        if (h > maxH)
        {
            float half = maxH / 2f;
            _panel.OffsetTop = -half;
            _panel.OffsetBottom = half;
        }
    }

    // ======================================================================
    // 卡牌列表构建
    // ======================================================================

    /// <summary>程序化构建卡牌库列表（首次打开触发，此后复用行控件），构建后默认选中第一张</summary>
    private void BuildList()
    {
        var listRoot = _panel.GetNode<VBoxContainer>("Margin/Root/Body/ListPanel/Scroll/CardList");
        foreach (var card in CardLibrary.CardList)
            listRoot.AddChild(CreateRow(card));
        _listBuilt = true;
        if (CardLibrary.CardList.Count > 0)
            SelectCard(CardLibrary.CardList[0].CardID);
    }

    /// <summary>构建一行卡牌：卡名 + 费用 + 数量 n/3 + [-] [+]；点击行选中并在右侧显示详情</summary>
    private HBoxContainer CreateRow(CardData card)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var nameLabel = new Label
        {
            Text = card.CardName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 24);
        row.AddChild(nameLabel);

        var costLabel = new Label { Text = $"{card.Cost}费" };
        costLabel.AddThemeFontSizeOverride("font_size", 22);
        costLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        row.AddChild(costLabel);

        var countLabel = new Label
        {
            Text = $"0/{MaxPerCard}",
            CustomMinimumSize = new Vector2(56, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        countLabel.AddThemeFontSizeOverride("font_size", 24);
        row.AddChild(countLabel);

        var minus = MakeStepButton("-");
        var plus = MakeStepButton("+");
        row.AddChild(minus);
        row.AddChild(plus);

        string id = card.CardID;
        plus.Pressed += () => ChangeCount(id, +1);
        minus.Pressed += () => ChangeCount(id, -1);
        // 行点击选中（子按钮自行消费点击，不会冒泡到这里）
        row.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                SelectCard(id);
        };
        _rows[id] = (nameLabel, countLabel, minus, plus);
        return row;
    }

    private static Button MakeStepButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(44, 36),
        };
        button.AddThemeFontSizeOverride("font_size", 26);
        button.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.75f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        return button;
    }

    // ======================================================================
    // 配置读写与刷新
    // ======================================================================

    /// <summary>从存档读取上次配置（无存档 = 空卡组）</summary>
    private void LoadFromSave()
    {
        _counts.Clear();
        foreach (var card in PlayerDeckSave.LoadCards())
        {
            _counts.TryGetValue(card.CardID, out int n);
            _counts[card.CardID] = n + 1;
        }
    }

    private int TotalCount()
    {
        int total = 0;
        foreach (var n in _counts.Values) total += n;
        return total;
    }

    /// <summary>按当前配置展开为卡牌模板列表（写盘用）</summary>
    private List<CardData> ExpandCards()
    {
        var cards = new List<CardData>();
        foreach (var pair in _counts)
        {
            if (CardLibrary.CardDictionary.TryGetValue(pair.Key, out var card))
            {
                for (int i = 0; i < pair.Value; i++)
                    cards.Add(card);
            }
        }
        return cards;
    }

    /// <summary>增删某张卡并实时保存（防重复保存：无变化不动作）</summary>
    private void ChangeCount(string cardId, int delta)
    {
        int n = _counts.TryGetValue(cardId, out var c) ? c : 0;
        int next = n + delta;
        if (next < 0 || next > MaxPerCard) return;
        if (delta > 0 && TotalCount() >= MaxDeckSize) return;

        if (next == 0) _counts.Remove(cardId);
        else _counts[cardId] = next;

        AudioManager.Instance?.PlayUiSfx("ui_click");
        PlayerDeckSave.SaveCards(ExpandCards());
        RefreshAll();
    }

    /// <summary>刷新所有行（数量文案 + 按钮置灰）+ 状态条</summary>
    private void RefreshAll()
    {
        int total = TotalCount();
        foreach (var pair in _rows)
        {
            int n = _counts.TryGetValue(pair.Key, out var c) ? c : 0;
            pair.Value.countLabel.Text = $"{n}/{MaxPerCard}";
            pair.Value.plus.Disabled = n >= MaxPerCard || total >= MaxDeckSize;
            pair.Value.minus.Disabled = n <= 0;
        }
        _statusLabel.Text = $"卡组 {total}/{MaxDeckSize} 张";
        _deckSummary.Text = BuildSummary();
    }

    /// <summary>按名称统计当前卡组为 "名字×数量" 紧凑文本（数量 1 不加后缀），保持选卡顺序</summary>
    private string BuildSummary()
    {
        if (_counts.Count == 0) return "当前卡组：（空）";
        var parts = new string[_counts.Count];
        int i = 0;
        foreach (var pair in _counts)
        {
            string name = CardLibrary.CardDictionary.TryGetValue(pair.Key, out var card) ? card.CardName : "?";
            parts[i] = pair.Value > 1 ? $"{name}×{pair.Value}" : name;
            i++;
        }
        return $"当前卡组：{string.Join("、", parts)}";
    }

    // ======================================================================
    // 选中与详情
    // ======================================================================

    private void SelectCard(string cardId)
    {
        if (_selectedId == cardId) return;
        _selectedId = cardId;
        RefreshRowHighlight();
        if (CardLibrary.CardDictionary.TryGetValue(cardId, out var card))
            ShowDetail(card);
    }

    /// <summary>选中行卡名高亮（白字），其余恢复半透明</summary>
    private void RefreshRowHighlight()
    {
        foreach (var pair in _rows)
        {
            bool selected = pair.Key == _selectedId;
            pair.Value.nameLabel.AddThemeColorOverride("font_color",
                selected ? Colors.White : new Color(1, 1, 1, 0.7f));
        }
    }

    /// <summary>填充右侧详情区（仿战斗内 UnitInfoPanel.ShowCard 的完整卡牌信息）</summary>
    private void ShowDetail(CardData data)
    {
        _detailName.Text = $"{data.CardName}　{CardTypeName(data.Type)}·{data.Rarity}";
        _detailDesc.Text = data.Description;

        string tags = data.Tags is { Count: > 0 } ? string.Join(", ", data.Tags) : "无";
        var filter = TargetFilter.CombineAnd(data.TargetFilters);
        var sb = new StringBuilder();
        sb.Append($"费用：{data.Cost}\n");
        sb.Append($"ID：{data.CardID}　世界：{data.World}　势力：{data.Faction}\n");
        sb.Append($"标签：{tags}\n");
        sb.Append($"目标：{TargetShapeText(filter)}");

        // 单位卡：附加召唤单位信息
        if (data is UnitCardData unitCard && unitCard.UnitData != null)
        {
            var ud = unitCard.UnitData;
            sb.Append($"\n\n【召唤单位】{ud.UnitName}　{ud.Type}·{ud.Rarity}\n");
            sb.Append($"HP：{ud.HealthPoints}　攻击力：{ud.AttackPower}　体力：{ud.Stamina}　射程：{CellShape.DescribeRange(ud.AttackShape, ud.AttackDistance)}　行动点：{ud.ActionPoints}\n");
            sb.Append($"{ud.Description}\n");
            sb.Append($"被动效果：{ud.PassiveEffects?.Length ?? 0} 个");
        }

        // 环境卡：附加环境信息
        if (data is EnvironmentCardData envCard && envCard.EnvironmentData != null)
        {
            var ed = envCard.EnvironmentData;
            sb.Append($"\n\n【环境】{ed.EnvironmentName}　{DurationText(ed.Duration)}\n");
            sb.Append($"{ed.Description}\n");
            sb.Append($"移动消耗修正：{ed.MoveCostDelta}");
        }

        // 装备卡：附加装备信息
        if (data is EquipmentCardData equipCard && equipCard.EquipmentData != null)
        {
            var eqd = equipCard.EquipmentData;
            sb.Append($"\n\n【装备】{eqd.EquipmentName}：{eqd.Description}\n");
            sb.Append($"加成：攻击+{eqd.AttackBonus} 生命+{eqd.MaxHealthBonus} 射程+{eqd.AttackDistanceBonus} " +
                      $"体力+{eqd.StaminaBonus} 行动点+{eqd.ActionPointBonus}");
        }
        _detailInfo.Text = sb.ToString();
    }

    private static string CardTypeName(CardType type) => type switch
    {
        CardType.Unit        => "单位卡",
        CardType.Spell       => "法术卡",
        CardType.Environment => "环境卡",
        CardType.Equipment   => "装备卡",
        _                    => "特殊卡",
    };

    private static string TargetShapeText(TargetFilter filter)
    {
        if (filter == null) return "无目标（直接打出）";
        return filter.GetShape() switch
        {
            TargetShape.None        => "无目标（直接打出）",
            TargetShape.All         => "全地图",
            TargetShape.SingleUnit  => "点选单位",
            TargetShape.SingleCell  => "点选格子",
            TargetShape.AreaDiamond => "菱形区域",
            TargetShape.AreaSquare  => "方形区域",
            TargetShape.Cross       => "十字区域",
            TargetShape.X           => "叉字区域",
            TargetShape.Ray         => "射线区域",
            TargetShape.Triangle    => "三角区域",
            TargetShape.Row         => "行区域",
            TargetShape.Column      => "列区域",
            TargetShape.Ring        => "环形区域",
            _                       => filter.GetShape().ToString(),
        };
    }

    private static string DurationText(int duration) => duration < 0 ? "永久" : $"持续 {duration} 回合";

    // ======================================================================
    // 面板动画（与设置/选关面板同款）
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
