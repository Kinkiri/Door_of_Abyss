using Godot;
using System;
[GlobalClass]
public partial class CreateCardAction : GameAction
{
    [Export] CardData CardData {  get; set; }
    [Export] int Count { get; set; }
    [Export] string CardListID { get; set; } // 从卡牌库的列表随机抽牌
    [Export] bool IsRandom { get; set; } = false;

    protected override void Apply(Context ctx)
    {
        if (IsRandom)
        {
            if (!string.IsNullOrEmpty(CardListID)) throw new Exception("随机牌库ID为空");
            // 暂时保留
        }
        for (int i = 0; i < Count; i++)
        {
            CardManager.Instance.CreateCard(CardData);
        }
    }
}
