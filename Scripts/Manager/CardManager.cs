using Godot;
using System.Collections.Generic;

/// <summary>
/// 卡牌管理器，纯逻辑：管理牌库、手牌、弃牌堆，三堆均持有 Card 运行时实例
/// </summary>
public partial class CardManager : Node
{
    public static CardManager Instance { get; private set; }

    /// <summary>当前手牌（Card 运行时实例）</summary>
    public List<Card> HandCards { get; private set; } = new();

    /// <summary>牌库（Card 运行时实例）</summary>
    public List<Card> DrawPile { get; private set; } = new();

    /// <summary>弃牌堆（Card 运行时实例）</summary>
    public List<Card> DiscardPile { get; private set; } = new();

    /// <summary>卡牌更新信号</summary>
    public event System.Action OnCardsUpdated;

    public void NotifyCardsUpdated() => OnCardsUpdated?.Invoke();

    public override void _Ready()
    {
        Instance = this;
    }

    public void Init() { }

    // ======================================================================
    // 初始化
    // ======================================================================

    /// <summary>用指定卡牌列表初始化牌库，自动创建 Card 实例并洗牌</summary>
    public void InitializeDrawPile(List<CardData> drawPile)
    {
        ClearAll();

        foreach (var cd in drawPile)
            DrawPile.Add(new Card(cd));

        ShuffleDrawPile();
        GD.Print($"[CardManager] 牌库初始化完成，共 {DrawPile.Count} 张");
        NotifyCardsUpdated();
    }

    /// <summary>从卡牌库中随机抽 count 张作为初始牌库</summary>
    public void InitializeDrawPile(int count)
    {
        ClearAll();

        var allCards = new List<CardData>(CardLibrary.CardList);
        var rng = new System.Random();
        for (int i = allCards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (allCards[i], allCards[j]) = (allCards[j], allCards[i]);
        }

        for (int i = 0; i < count && i < allCards.Count; i++)
            DrawPile.Add(new Card(allCards[i]));

        GD.Print($"[CardManager] 牌库初始化完成，共 {DrawPile.Count} 张");
        NotifyCardsUpdated();
    }

    // ======================================================================
    // 抽牌 / 洗牌
    // ======================================================================

    /// <summary>确保牌库有牌：空时从弃牌堆洗回，都空则从卡牌库新建</summary>
    private void EnsureDrawPile()
    {
        if (DrawPile.Count > 0) return;

        if (DiscardPile.Count > 0)
        {
            GD.Print("[CardManager] 牌库为空，将弃牌堆洗回牌库");
            DrawPile.AddRange(DiscardPile);
            DiscardPile.Clear();
            ShuffleDrawPile();
        }
        else
        {
            // 终极保障：牌库和弃牌堆均为空时从全部已加载卡牌重新建库。
            // 用于开发调试阶段避免抽牌中断；正式发布时建议移除或限制次数
            GD.Print("[CardManager] 牌库和弃牌堆均为空，从卡牌库新建");
            InitializeDrawPile(CardLibrary.CardList.Count);
        }
    }

    /// <summary>抽取多张牌</summary>
    public List<Card> DrawCards(int count)
    {
        var drawn = new List<Card>();
        for (int i = 0; i < count; i++)
        {
            var card = DrawCard();
            if (card != null)
                drawn.Add(card);
        }
        return drawn;
    }

    /// <summary>从牌库顶抽一张牌到手牌，返回 Card 实例</summary>
    public Card DrawCard()
    {
        EnsureDrawPile();

        if (DrawPile.Count == 0) return null;

        var card = DrawPile[0];
        DrawPile.RemoveAt(0);
        HandCards.Add(card);
        GD.Print($"[CardManager] 抽牌: [{card.CardID}] {card.CardName}  手牌={HandCards.Count}");
        NotifyCardsUpdated();
        return card;
    }

    public Card CreateCard(CardData cardData)
    {
        var card = new Card(cardData);
        GD.Print($"[CardManager] 创建卡牌: [{card.CardID}] {card.CardName}");
        return card;
    }

    /// <summary>打乱牌库</summary>
    public void ShuffleDrawPile()
    {
        var rng = new System.Random();
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }
    }

    // ======================================================================
    // 弃牌 / 使用
    // ======================================================================

    /// <summary>弃掉一张手牌</summary>
    public void DiscardCard(Card card)
    {
        if (HandCards.Remove(card))
        {
            DiscardPile.Add(card);
            GD.Print($"[CardManager] 弃牌: [{card.CardID}] {card.CardName}");
            NotifyCardsUpdated();
        }
    }

    /// <summary>使用一张手牌</summary>
    public void UseCard(Card card)
    {
        if (HandCards.Remove(card))
        {
            DiscardPile.Add(card);
            GD.Print($"[CardManager] 使用卡牌: [{card.CardID}] {card.CardName}");
            NotifyCardsUpdated();
        }
        else
        {
            GD.PrintErr($"[CardManager] 无法使用卡牌: [{card.CardID}] {card.CardName}");
        }
    }

    // ======================================================================
    // 内部
    // ======================================================================

    private void ClearAll()
    {
        DrawPile.Clear();
        DiscardPile.Clear();
        HandCards.Clear();
    }
}
