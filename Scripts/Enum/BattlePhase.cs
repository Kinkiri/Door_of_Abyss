using Godot;
using System;

public enum BattlePhase
{
    GameStart,
    RoundStart,
    PlayerAction,
    EnemyAction,
    RoundEnd,
    GameEnd
}

public enum Team
{
    Neutral,
    Player,
    Enemy,
}
