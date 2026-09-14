using UnityEngine;

// Solo VIP progression. The panel is the easy half; the work is that every
// perk is actually read by the system it affects.
public static class ClubService
{
    static bool granting;

    public static void Init()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        Recalculate();
    }

    static void OnGameEvent(GameEvent e)
    {
        switch (e.Type)
        {
            case GameEventType.WheelSpun:
                AddPoints(50);
                break;
            case GameEventType.SlotSpun:
                AddPoints(Mathf.Max(1, (int)(e.Amount / 1000)));
                break;
            case GameEventType.ShopPurchase:
                AddPoints(500);
                break;
        }
        // Quest, set and puzzle payouts carry club points in their own Reward,
        // so they are deliberately not counted again here.
    }

    public static int Tier
    {
        get
        {
            var s = GameState.I;
            if (s == null) return 0;
            return Mathf.Clamp(s.Data.clubTier, 0, Content.ClubTiers.Length - 1);
        }
    }

    public static ClubTierDef Current => Content.ClubTiers[Tier];

    public static ClubTierDef Next =>
        Tier + 1 < Content.ClubTiers.Length ? Content.ClubTiers[Tier + 1] : null;

    public static bool IsMaxTier => Next == null;

    public static long Points => GameState.I != null ? GameState.I.Data.clubPoints : 0;

    public static float CoinMultiplier => Current.coinMultiplier;
    public static float WheelMultiplier => Current.wheelMultiplier;
    public static int ExtraQuestSlots => Current.extraQuestSlots;
    public static int DailyGemStipend => Current.dailyGemStipend;

    public static long PointsToNext
    {
        get
        {
            var next = Next;
            return next == null ? 0 : Mathf.Max(0, (int)(next.points - Points));
        }
    }

    public static float Progress01
    {
        get
        {
            var next = Next;
            if (next == null) return 1f;
            long floor = Current.points;
            long span = next.points - floor;
            if (span <= 0) return 1f;
            return Mathf.Clamp01((Points - floor) / (float)span);
        }
    }

    public static void AddPoints(long amount)
    {
        var s = GameState.I;
        if (s == null || amount <= 0) return;
        s.Data.clubPoints += amount;
        s.Save();
        Recalculate();
    }

    // Tier never decreases — points are a lifetime total, not a balance.
    static void Recalculate()
    {
        var s = GameState.I;
        if (s == null || granting) return;

        int tier = 0;
        for (int i = 0; i < Content.ClubTiers.Length; i++)
            if (s.Data.clubPoints >= Content.ClubTiers[i].points) tier = i;

        if (tier <= s.Data.clubTier) return;

        // A tier-up reward can itself award points; the guard stops that from
        // re-entering the promotion path mid-way.
        granting = true;
        try
        {
            while (s.Data.clubTier < tier)
            {
                s.Data.clubTier++;
                GameEvents.Raise(GameEventType.ClubTierUp, s.Data.clubTier);
            }
            s.Save();
        }
        finally
        {
            granting = false;
        }
    }
}
