using System;
using UnityEngine;

// All authored game content, as plain static data.
//
// Deliberately not ScriptableObjects: the whole set is fixed, offline and a
// few kilobytes, so there is nothing to load, nothing to lose to a broken
// GUID, and balance edits are a recompile rather than an asset hunt. Moving to
// ScriptableObjects later is a drop-in if inspector tuning becomes worth it.

public enum QuestGoal
{
    DailyLogin, SpinWheel, SlotSpins, WinCoins, BetTotal, OpenPacks,
    CollectCards, CompleteSet, EarnPuzzlePieces, SpendGems, LevelUp, ClaimMail
}

[Serializable]
public class QuestDef
{
    public string id;
    public string title;
    public QuestGoal goal;
    public long target;
    public Reward reward;
    public bool weekly;
    public bool needsSlotGame;
}

[Serializable]
public class CardDef
{
    public int id;
    public string cardName;
    public int rarity;       // 1..5 stars
    public int setId;
    public int symbolIndex;  // into ContentRefs.cardSymbols
}

[Serializable]
public class CardSetDef
{
    public int setId;
    public string setName;
    public Reward reward;
}

[Serializable]
public class ClubTierDef
{
    public string tierName;
    public long points;
    public float coinMultiplier;
    public float wheelMultiplier;
    public int extraQuestSlots;
    public int dailyGemStipend;
    public Color crestColor;
    public Reward tierUpReward;
}

[Serializable]
public class PuzzleDef
{
    public string id;
    public string title;
    public int imageIndex;
    public int rows;
    public int cols;
    public Reward reward;

    public int PieceCount => rows * cols;
}

[Serializable]
public class NewsDef
{
    public string id;
    public string title;
    public string body;
    public string sender;
    public int afterDays;    // days since the save was created
    public Reward reward;
}

public static class Content
{
    /// Flip when the slot scene exists; turns on every spin-driven quest.
    public static bool SlotGameAvailable = true;

    public const int QuestsPerDay = 3;

    // ---- quests --------------------------------------------------------

    public static readonly QuestDef[] Quests =
    {
        Q("d_login",   "OPEN THE GAME",             QuestGoal.DailyLogin,       1,      Reward.Of(coins: 25_000, clubPoints: 25)),
        Q("d_wheel",   "SPIN THE DAILY WHEEL",      QuestGoal.SpinWheel,        1,      Reward.Of(coins: 15_000, clubPoints: 50)),
        Q("d_pack",    "OPEN A CARD PACK",          QuestGoal.OpenPacks,        1,      Reward.Of(coins: 10_000, clubPoints: 25)),
        Q("d_cards3",  "COLLECT 3 CARDS",           QuestGoal.CollectCards,     3,      Reward.Of(coins: 20_000, clubPoints: 40)),
        Q("d_piece",   "EARN A PUZZLE PIECE",       QuestGoal.EarnPuzzlePieces, 1,      Reward.Of(coins: 15_000, clubPoints: 30)),
        Q("d_mail2",   "CLAIM 2 REWARDS FROM MAIL", QuestGoal.ClaimMail,        2,      Reward.Of(coins: 10_000, clubPoints: 20)),
        Q("d_gems20",  "SPEND 20 GEMS",             QuestGoal.SpendGems,        20,     Reward.Of(coins: 30_000, clubPoints: 60)),
        Q("d_win50k",  "WIN 50,000 COINS",          QuestGoal.WinCoins,         50_000, Reward.Of(coins: 20_000, cardPacks: 1, clubPoints: 40)),
        Q("d_level",   "GAIN A LEVEL",              QuestGoal.LevelUp,          1,      Reward.Of(coins: 40_000, gems: 10, clubPoints: 75)),

        // Enabled by Content.SlotGameAvailable once the slot scene ships.
        Q("d_spin25",  "PLAY 25 ROUNDS",            QuestGoal.SlotSpins,        25,      Reward.Of(coins: 30_000, clubPoints: 50), slot: true),
        Q("d_spin100", "PLAY 100 ROUNDS",           QuestGoal.SlotSpins,        100,     Reward.Of(coins: 75_000, cardPacks: 1, clubPoints: 120), slot: true),
        Q("d_bet250k", "BET 250,000 IN TOTAL",      QuestGoal.BetTotal,         250_000, Reward.Of(coins: 50_000, clubPoints: 80), slot: true),

        // ---- weekly ----
        Q("w_wheel5",  "SPIN THE WHEEL 5 DAYS",     QuestGoal.SpinWheel,        5,       Reward.Of(coins: 150_000, gems: 25, clubPoints: 250), weekly: true),
        Q("w_login5",  "PLAY ON 5 DIFFERENT DAYS",  QuestGoal.DailyLogin,       5,       Reward.Of(coins: 200_000, gems: 30, clubPoints: 300), weekly: true),
        Q("w_cards15", "COLLECT 15 CARDS",          QuestGoal.CollectCards,     15,      Reward.Of(coins: 100_000, cardPacks: 2, clubPoints: 200), weekly: true),
        Q("w_piece5",  "EARN 5 PUZZLE PIECES",      QuestGoal.EarnPuzzlePieces, 5,       Reward.Of(coins: 120_000, gems: 20, clubPoints: 220), weekly: true),
        Q("w_set",     "COMPLETE A CARD SET",       QuestGoal.CompleteSet,      1,       Reward.Of(coins: 250_000, gems: 40, clubPoints: 400), weekly: true),
        Q("w_spin500", "PLAY 500 ROUNDS",           QuestGoal.SlotSpins,        500,     Reward.Of(coins: 400_000, gems: 50, clubPoints: 500), weekly: true, slot: true)
    };

    static QuestDef Q(string id, string title, QuestGoal goal, long target, Reward reward,
                      bool weekly = false, bool slot = false)
    {
        return new QuestDef
        {
            id = id, title = title, goal = goal, target = target,
            reward = reward, weekly = weekly, needsSlotGame = slot
        };
    }

    // ---- cards ---------------------------------------------------------

    public static readonly CardSetDef[] CardSets =
    {
        new CardSetDef { setId = 0, setName = "BAMBOO GROVE",  reward = Reward.Of(coins: 250_000, gems: 25, puzzlePieces: 1, clubPoints: 500) },
        new CardSetDef { setId = 1, setName = "GOLDEN TEMPLE", reward = Reward.Of(coins: 400_000, gems: 40, puzzlePieces: 1, clubPoints: 700) },
        new CardSetDef { setId = 2, setName = "LUCKY CHARMS",  reward = Reward.Of(coins: 600_000, gems: 60, puzzlePieces: 2, clubPoints: 900) },
        new CardSetDef { setId = 3, setName = "HIGH ROLLERS",  reward = Reward.Of(coins: 900_000, gems: 90, puzzlePieces: 2, clubPoints: 1200) },
        new CardSetDef { setId = 4, setName = "JADE DYNASTY",  reward = Reward.Of(coins: 1_500_000, gems: 150, puzzlePieces: 3, clubPoints: 2000) }
    };

    public const int CardsPerSet = 9;

    // Position within a set determines rarity, so every set has the same shape:
    // three commons, two uncommons, two rares, one epic, one legendary.
    static readonly int[] RarityBySlot = { 1, 1, 1, 2, 2, 3, 3, 4, 5 };

    static readonly string[] CardNames =
    {
        "SPROUT", "SHOOT", "STALK", "GROVE KEEPER", "MORNING DEW",
        "RAIN DANCE", "ELDER BAMBOO", "GREEN SENTINEL", "BAMBOO SPIRIT",

        "STONE STEP", "BRASS BELL", "RED LANTERN", "TEMPLE GATE", "INCENSE COIL",
        "PRAYER SCROLL", "GOLDEN ROOF", "GUARDIAN LION", "TEMPLE HEART",

        "COPPER COIN", "RED ENVELOPE", "PAPER FAN", "JADE RING", "SILK KNOT",
        "FORTUNE CAT", "WISHING DRUM", "DOUBLE HAPPINESS", "EIGHT TREASURES",

        "FIRST CHIP", "LUCKY SEVEN", "SPLIT PAIR", "HOT STREAK", "TABLE BOSS",
        "VELVET ROPE", "PRIVATE ROOM", "WHALE WATCH", "HOUSE LEGEND",

        "JADE SHARD", "INK BRUSH", "SILK ROAD", "EMPEROR'S SEAL", "MOON GATE",
        "PHOENIX FEATHER", "DRAGON SCALE", "CELESTIAL MAP", "JADE THRONE"
    };

    public static readonly CardDef[] Cards = BuildCards();

    static CardDef[] BuildCards()
    {
        var list = new CardDef[CardSets.Length * CardsPerSet];
        for (int i = 0; i < list.Length; i++)
        {
            int slot = i % CardsPerSet;
            list[i] = new CardDef
            {
                id = i,
                cardName = i < CardNames.Length ? CardNames[i] : "CARD " + i,
                rarity = RarityBySlot[slot],
                setId = i / CardsPerSet,
                symbolIndex = i % 7
            };
        }
        return list;
    }

    /// Duplicate payout by rarity, indexed 1..5.
    public static readonly long[] DuplicateCoins = { 0, 5_000, 12_000, 30_000, 90_000, 250_000 };

    /// Relative drop weight by rarity, indexed 1..5.
    public static readonly int[] RarityWeights = { 0, 45, 28, 17, 8, 2 };

    public const int CardsPerPack = 3;

    // ---- club ----------------------------------------------------------

    public static readonly ClubTierDef[] ClubTiers =
    {
        new ClubTierDef { tierName = "BRONZE",   points = 0,       coinMultiplier = 1.00f, wheelMultiplier = 1.00f, extraQuestSlots = 0, dailyGemStipend = 0,  crestColor = new Color(0.80f, 0.50f, 0.28f), tierUpReward = Reward.Of() },
        new ClubTierDef { tierName = "SILVER",   points = 2_500,   coinMultiplier = 1.05f, wheelMultiplier = 1.25f, extraQuestSlots = 0, dailyGemStipend = 5,  crestColor = new Color(0.78f, 0.80f, 0.86f), tierUpReward = Reward.Of(coins: 100_000, gems: 20, cardPacks: 1) },
        new ClubTierDef { tierName = "GOLD",     points = 10_000,  coinMultiplier = 1.15f, wheelMultiplier = 1.50f, extraQuestSlots = 1, dailyGemStipend = 15, crestColor = new Color(1f, 0.82f, 0.35f),    tierUpReward = Reward.Of(coins: 300_000, gems: 50, cardPacks: 2) },
        new ClubTierDef { tierName = "PLATINUM", points = 30_000,  coinMultiplier = 1.25f, wheelMultiplier = 1.75f, extraQuestSlots = 1, dailyGemStipend = 30, crestColor = new Color(0.70f, 0.92f, 1f),    tierUpReward = Reward.Of(coins: 750_000, gems: 100, cardPacks: 3, puzzlePieces: 1) },
        new ClubTierDef { tierName = "DIAMOND",  points = 100_000, coinMultiplier = 1.40f, wheelMultiplier = 2.00f, extraQuestSlots = 2, dailyGemStipend = 50, crestColor = new Color(0.62f, 0.85f, 1f),    tierUpReward = Reward.Of(coins: 2_000_000, gems: 250, cardPacks: 5, puzzlePieces: 2) }
    };

    // ---- puzzles -------------------------------------------------------

    public static readonly PuzzleDef[] Puzzles =
    {
        new PuzzleDef { id = "p_paradise", title = "PANDA PARADISE", imageIndex = 0, rows = 3, cols = 3, reward = Reward.Of(coins: 500_000,   gems: 100, cardPacks: 2, clubPoints: 800) },
        new PuzzleDef { id = "p_pagoda",   title = "GOLDEN PAGODA",  imageIndex = 1, rows = 3, cols = 3, reward = Reward.Of(coins: 750_000,   gems: 150, cardPacks: 3, clubPoints: 1200) },
        new PuzzleDef { id = "p_fortune",  title = "JADE FORTUNE",   imageIndex = 2, rows = 4, cols = 4, reward = Reward.Of(coins: 1_500_000, gems: 250, cardPacks: 5, clubPoints: 2500) }
    };

    // ---- scheduled mail ------------------------------------------------

    public static readonly NewsDef[] News =
    {
        new NewsDef { id = "n_welcome", afterDays = 0, sender = "ULTRA PANDA", title = "WELCOME!",
            body = "Thanks for playing. Here is something to get you started — spin the daily wheel every day for more.",
            reward = Reward.Of(coins: 50_000, gems: 10) },

        new NewsDef { id = "n_cards", afterDays = 1, sender = "ULTRA PANDA", title = "START COLLECTING",
            body = "Card packs drop from quests, the wheel and levelling up. Finish a set for a big payout.",
            reward = Reward.Of(cardPacks: 2) },

        new NewsDef { id = "n_puzzle", afterDays = 3, sender = "ULTRA PANDA", title = "PIECE IT TOGETHER",
            body = "Puzzle pieces come from quests and completed card sets. Fill the board to win the jackpot.",
            reward = Reward.Of(puzzlePieces: 2, coins: 50_000) },

        new NewsDef { id = "n_club", afterDays = 5, sender = "PANDA CLUB", title = "CLUB PERKS",
            body = "Club points build with everything you play. Higher tiers pay bigger wheel rewards and a daily gem stipend.",
            reward = Reward.Of(clubPoints: 500, coins: 75_000) },

        new NewsDef { id = "n_week1", afterDays = 7, sender = "ULTRA PANDA", title = "ONE WEEK IN",
            body = "A week of spins. Here is a thank you from all of us at the bamboo table.",
            reward = Reward.Of(coins: 250_000, gems: 50, cardPacks: 2) }
    };

    /// Login-streak milestones, delivered as mail on the day they are reached.
    public static Reward StreakReward(int streak)
    {
        switch (streak)
        {
            case 3:  return Reward.Of(coins: 50_000, clubPoints: 100);
            case 5:  return Reward.Of(coins: 100_000, gems: 15, clubPoints: 150);
            case 7:  return Reward.Of(coins: 200_000, gems: 30, cardPacks: 1, clubPoints: 250);
            case 14: return Reward.Of(coins: 500_000, gems: 60, cardPacks: 2, puzzlePieces: 1, clubPoints: 500);
            case 30: return Reward.Of(coins: 1_500_000, gems: 150, cardPacks: 5, puzzlePieces: 2, clubPoints: 1500);
            default: return Reward.Of();
        }
    }

    public static QuestDef Quest(string id)
    {
        for (int i = 0; i < Quests.Length; i++)
            if (Quests[i].id == id) return Quests[i];
        return null;
    }

    public static CardDef Card(int id) =>
        id >= 0 && id < Cards.Length ? Cards[id] : null;

    public static CardSetDef Set(int setId) =>
        setId >= 0 && setId < CardSets.Length ? CardSets[setId] : null;
}
