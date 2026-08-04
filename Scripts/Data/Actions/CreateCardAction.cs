using Godot;
using System;
using System.Linq;
[GlobalClass]
public partial class CreateCardAction : GameAction
{
    [Export] CardData CardData {  get; set; }
    [Export] int Count { get; set; }
    [Export] bool IsRandom { get; set; } = false;
    //随机创建牌筛选器
    [Export] CardFilter[] Filters { get; set; }
    //启用后，创建的卡牌会直接放入玩家的牌库，而不是手牌
    [Export] bool ToDeck { get; set; } = false;

    protected override void Apply(Context ctx)
    {
        if (IsRandom)
        {
            var filter = CardFilter.CombineAnd(Filters);

            // 候选 = 匹配筛选的卡牌模板
            var pool = CardLibrary.GetCards(filter)
                .OfType<CardData>()
                .ToArray();

            for (var i = 0; i < Count; i++)
            {
                var picked = pool[GD.Randi() % pool.Length];
                CardManager.Instance.CreateCard(picked, ToDeck);
            }
        }
        for (int i = 0; i < Count; i++)
        {
            CardManager.Instance.CreateCard(CardData, ToDeck);
        }
    }
}
