using UnityEngine;

// Piggy bank: a slice of every bet drips into a locked pot the player watches
// fill. Smash it free once it's full, or pay gems to smash early. Pure
// retention — it only fattens while they keep spinning, and it never touches
// the win economy (deposits come out of bets already spent, not out of thin
// air), so it doesn't need to be balanced against the machines' RTP.
public static class PiggyService
{
    public const long Capacity = 500_000;   // full pot
    public const float FillRate = 0.05f;     // 5% of each bet drips in
    public const int EarlyBreakGemCost = 60; // smash before full

    public static void Init()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
    }

    static void OnGameEvent(GameEvent e)
    {
        if (e.Type == GameEventType.SlotSpun) Deposit(e.Amount);
    }

    public static long Balance => GameState.I != null ? GameState.I.Data.piggyBalance : 0;
    public static bool IsFull => Balance >= Capacity;
    public static float Fill01 => Capacity <= 0 ? 0f : Mathf.Clamp01((float)Balance / Capacity);

    static void Deposit(long bet)
    {
        var s = GameState.I;
        if (s == null || s.Data.piggyBalance >= Capacity) return;
        long add = (long)(bet * FillRate);
        if (add <= 0) return;
        s.Data.piggyBalance = System.Math.Min(Capacity, s.Data.piggyBalance + add);
        s.Save();
    }

    /// Smash a full piggy (free). Returns coins collected, 0 if not full.
    public static long Break()
    {
        var s = GameState.I;
        if (s == null || !IsFull) return 0;
        return Smash(s);
    }

    /// Smash early for gems. Returns coins collected, or 0 if it couldn't —
    /// the piggy is empty, already full (use Break), or gems are short.
    public static long BreakEarly()
    {
        var s = GameState.I;
        if (s == null || IsFull || s.Data.piggyBalance <= 0) return 0;
        if (!s.TrySpendGems(EarlyBreakGemCost)) return 0;
        return Smash(s);
    }

    static long Smash(GameState s)
    {
        long amount = s.Data.piggyBalance;
        s.Data.piggyBalance = 0;
        s.AddCoins(amount, RewardSource.Piggy); // credits coins, saves, updates HUD
        return amount;
    }
}
