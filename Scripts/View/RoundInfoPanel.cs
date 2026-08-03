using Godot;

/// <summary>
/// 右上角战斗信息面板（View 层）：阶段、当前阵营、回合、费用、手牌数 + 结束回合按钮。
/// 挂在预制体根节点（PanelContainer），子节点全部用 [Export] 引用。
/// 订阅 BattleManager 的 PhaseChanged/GameEnded/CostChanged 与 CardManager.OnCardsUpdated 刷新。
/// 取代旧 RoundView（裸 Label 绝对定位，费用与回合位置重叠）。
/// </summary>
public partial class RoundInfoPanel : PanelContainer
{
    [Export] public Label PhaseLabel;
    [Export] public Label TeamLabel;
    [Export] public Label RoundLabel;
    [Export] public Label CostLabel;
    [Export] public Label HandCountLabel;
    [Export] public Button EndTurnButton;

    public override void _Ready()
    {
        var bm = BattleManager.Instance;
        if (bm == null)
        {
            GD.PrintErr("RoundInfoPanel: BattleManager.Instance 为空");
            return;
        }

        bm.PhaseChanged += OnPhaseChanged;
        bm.GameEnded += OnGameEnded;
        bm.CostChanged += OnCostChanged;
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsUpdated += OnCardsUpdated;

        if (EndTurnButton != null)
            EndTurnButton.Pressed += OnEndTurnPressed;

        UpdateDisplay(bm.CurrentPhase, bm.CurrentTeam, bm.RoundCount);
        OnCostChanged(bm.PlayerCost, BattleManager.MaxCost);
        OnCardsUpdated();
    }

    public override void _ExitTree()
    {
        var bm = BattleManager.Instance;
        if (bm != null)
        {
            bm.PhaseChanged -= OnPhaseChanged;
            bm.GameEnded -= OnGameEnded;
            bm.CostChanged -= OnCostChanged;
        }
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsUpdated -= OnCardsUpdated;
        if (EndTurnButton != null)
            EndTurnButton.Pressed -= OnEndTurnPressed;
    }

    // ======================================================================
    // 刷新
    // ======================================================================

    private void OnPhaseChanged(BattlePhase newPhase, Team currentTeam, int round)
    {
        UpdateDisplay(newPhase, currentTeam, round);
    }

    private void OnGameEnded(Team winner, int round)
    {
        if (PhaseLabel != null)
            PhaseLabel.Text = $"游戏结束！{WinnerName(winner)}胜利";
        if (TeamLabel != null)
            TeamLabel.Text = "";
    }

    private void OnCostChanged(int current, int max)
    {
        if (CostLabel != null)
            CostLabel.Text = $"费用：{current}/{max}";
    }

    private void OnCardsUpdated()
    {
        if (HandCountLabel != null)
            HandCountLabel.Text = $"手牌：{CardManager.Instance?.HandCards.Count ?? 0} 张";
    }

    private void UpdateDisplay(BattlePhase phase, Team team, int round)
    {
        if (PhaseLabel != null)
            PhaseLabel.Text = PhaseName(phase);
        if (TeamLabel != null)
        {
            TeamLabel.Text = team == Team.Neutral ? "" : $"{TeamName(team)}回合";
            // 阵营着色：玩家蓝、敌方红
            TeamLabel.Modulate = team switch
            {
                Team.Player => new Color(0.45f, 0.8f, 1f),
                Team.Enemy  => new Color(1f, 0.45f, 0.45f),
                _           => Colors.White,
            };
        }
        if (RoundLabel != null)
            RoundLabel.Text = $"回合：{round}";
    }

    /// <summary>结束回合：推进战斗到下一阶段（绑定到面板内按钮）</summary>
    private void OnEndTurnPressed()
    {
        BattleManager.Instance?.AdvancePhase();
    }

    // ======================================================================
    // 文本映射（沿用原 RoundView）
    // ======================================================================

    private static string PhaseName(BattlePhase phase) => phase switch
    {
        BattlePhase.GameStart    => "放门！",
        BattlePhase.RoundStart   => "回合开始",
        BattlePhase.PlayerAction => "行动阶段",
        BattlePhase.EnemyAction  => "敌方行动",
        BattlePhase.RoundEnd     => "回合结束",
        BattlePhase.GameEnd      => "游戏结束",
        _                        => phase.ToString(),
    };

    private static string TeamName(Team team) => team switch
    {
        Team.Player => "玩家",
        Team.Enemy  => "敌方",
        _           => "",
    };

    private static string WinnerName(Team team) => team switch
    {
        Team.Player => "玩家方",
        Team.Enemy  => "敌方",
        _           => "无",
    };
}
