using System.Collections.Generic;
using UnityEngine;

public struct CardDrop
{
    public int cardId;
    public bool isNew;
    public long duplicateCoins;
}

// Card album. Packs are opened here; the panel only animates what this returns.
public static class CardService
{
    public static void Init()
    {
        EnsureArray();
    }

    static void EnsureArray()
    {
        var s = GameState.I;
        if (s == null) return;
        int need = Content.Cards.Length;
        if (s.Data.cardCounts == null || s.Data.cardCounts.Length != need)
        {
            var grown = new int[need];
            var old = s.Data.cardCounts;
            if (old != null)
                for (int i = 0; i < old.Length && i < need; i++) grown[i] = old[i];
            s.Data.cardCounts = grown;
            s.Save();
        }
    }

    public static int PackCount => GameState.I != null ? GameState.I.Data.cardPacks : 0;

    public static void AddPacks(int count)
    {
        var s = GameState.I;
        if (s == null || count <= 0) return;
        s.Data.cardPacks += count;
        s.Save();
        Notifications.Bump();
    }

    public static int CountOf(int cardId)
    {
        var s = GameState.I;
        if (s == null || s.Data.cardCounts == null) return 0;
        if (cardId < 0 || cardId >= s.Data.cardCounts.Length) return 0;
        return s.Data.cardCounts[cardId];
    }

    public static bool Owns(int cardId) => CountOf(cardId) > 0;

    public static int OwnedInSet(int setId)
    {
        int n = 0;
        for (int i = 0; i < Content.Cards.Length; i++)
            if (Content.Cards[i].setId == setId && Owns(i)) n++;
        return n;
    }

    public static bool SetComplete(int setId) => OwnedInSet(setId) >= Content.CardsPerSet;

    public static bool SetClaimed(int setId)
    {
        var s = GameState.I;
        return s != null && s.Data.completedSets != null && s.Data.completedSets.Contains(setId);
    }

    public static bool SetClaimable(int setId) => SetComplete(setId) && !SetClaimed(setId);

    public static bool ClaimSet(int setId)
    {
        var s = GameState.I;
        if (s == null || !SetClaimable(setId)) return false;
        var def = Content.Set(setId);
        if (def == null) return false;

        s.Data.completedSets.Add(setId);
        s.Save();
        RewardService.Grant(def.reward, RewardSource.Cards, def.setName + " COMPLETE!");
        GameEvents.Raise(GameEventType.SetCompleted, 1, setId);
        return true;
    }

    /// Packs waiting to open, plus sets sitting finished but unclaimed.
    public static int AttentionCount
    {
        get
        {
            int n = PackCount;
            for (int i = 0; i < Content.CardSets.Length; i++)
                if (SetClaimable(Content.CardSets[i].setId)) n++;
            return n;
        }
    }

    // ---- opening -------------------------------------------------------

    public static CardDrop[] OpenPack()
    {
        var s = GameState.I;
        if (s == null || s.Data.cardPacks <= 0) return null;

        EnsureArray();
        s.Data.cardPacks--;

        var drops = new CardDrop[Content.CardsPerPack];
        for (int i = 0; i < drops.Length; i++)
            drops[i] = DrawOne();

        s.Save();
        GameEvents.Raise(GameEventType.PackOpened, 1);
        for (int i = 0; i < drops.Length; i++)
            GameEvents.Raise(GameEventType.CardObtained, 1, drops[i].cardId);

        Notifications.Bump();
        return drops;
    }

    static readonly List<int> Candidates = new List<int>();

    static CardDrop DrawOne()
    {
        int rarity = RollRarity();
        int cardId = PickInRarity(rarity);

        var s = GameState.I;
        var drop = new CardDrop { cardId = cardId, isNew = !Owns(cardId) };

        s.Data.cardCounts[cardId]++;

        if (!drop.isNew)
        {
            var def = Content.Card(cardId);
            long coins = Content.DuplicateCoins[Mathf.Clamp(def.rarity, 0, Content.DuplicateCoins.Length - 1)];
            drop.duplicateCoins = coins;
            if (coins > 0) s.AddCoins(coins, RewardSource.Cards);
        }

        return drop;
    }

    /// Exposed for the self-check, which samples the distribution.
    public static int PreviewRarityRoll() => RollRarity();

    static int RollRarity()
    {
        int total = 0;
        for (int r = 1; r < Content.RarityWeights.Length; r++) total += Content.RarityWeights[r];
        int roll = Random.Range(0, total);
        for (int r = 1; r < Content.RarityWeights.Length; r++)
        {
            roll -= Content.RarityWeights[r];
            if (roll < 0) return r;
        }
        return 1;
    }

    // Within a rarity, cards the player is missing are three times as likely.
    // Without that nudge the last card of a set can stall indefinitely, which
    // is exactly where players give up.
    static int PickInRarity(int rarity)
    {
        Candidates.Clear();
        var cards = Content.Cards;
        for (int i = 0; i < cards.Length; i++)
            if (cards[i].rarity == rarity) Candidates.Add(i);

        if (Candidates.Count == 0) return 0;

        int total = 0;
        for (int i = 0; i < Candidates.Count; i++)
            total += WeightOf(Candidates[i]);

        int roll = Random.Range(0, total);
        for (int i = 0; i < Candidates.Count; i++)
        {
            roll -= WeightOf(Candidates[i]);
            if (roll < 0) return Candidates[i];
        }
        return Candidates[0];
    }

    static int WeightOf(int cardId)
    {
        if (Owns(cardId)) return 1;
        var def = Content.Card(cardId);
        // An unowned card in a set the player is already working on is the most
        // useful thing the pack can contain.
        return def != null && !SetComplete(def.setId) ? 3 : 2;
    }
}
