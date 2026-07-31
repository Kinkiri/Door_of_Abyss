using Godot;
using System;

public partial class RoundView : Node
{
    [Export] public Label PhaseLabel;
    [Export] public Label RoundLabel;
    [Export] public Label TeamLabel;
    [Export] public Label CostLabel;

    public override void _Ready()
    {
        var bm = BattleManager.Instance;
        if (bm == null)
        {
            GD.PrintErr("RoundView: BattleManager.Instance 为空");
            return;
        }

        bm.PhaseChanged += OnPhaseChanged;
        bm.GameEnded += OnGameEnded;
        bm.CostChanged += OnCostChanged;

        UpdateDisplay(bm.CurrentPhase, bm.CurrentTeam, bm.RoundCount);
        OnCostChanged(bm.PlayerCost, BattleManager.MaxCost);
    }

    public override void _ExitTree()
    {
        var bm = BattleManager.Instance;
        if (bm == null) return;

        bm.PhaseChanged -= OnPhaseChanged;
        bm.GameEnded -= OnGameEnded;
        bm.CostChanged -= OnCostChanged;
    }

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
            CostLabel.Text = $"费用 {current}/{max}";
    }

    private void UpdateDisplay(BattlePhase phase, Team team, int round)
    {
        if (PhaseLabel != null)
            PhaseLabel.Text = PhaseName(phase);
        if (RoundLabel != null)
            RoundLabel.Text = $"回合 {round}";
        if (TeamLabel != null)
            TeamLabel.Text = team == Team.Neutral ? "" : $"{TeamName(team)}回合";
    }

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

    /// <summary>
    /// 手动推进战斗到下一阶段。绑定到 UI 按钮（"结束回合"），
    /// 在 PlayerAction 阶段由玩家主动触发 AdvancePhase。
    /// </summary>
    public void _on_button_button_down()
    {
        BattleManager.Instance?.AdvancePhase();
    }
}
