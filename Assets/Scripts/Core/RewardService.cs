using System;
using UnityEngine;

// Single place a Reward turns into actual player property, so every feature
// pays out the same way and the celebration popup fires from one hook.
public static class RewardService
{
    /// Reward and an optional headline for the popup.
    public static event Action<Reward, string> Granted;

    public static void Grant(Reward r, RewardSource src, string headline = null)
    {
        var s = GameState.I;
        if (s == null || r.IsEmpty) return;

        if (r.coins > 0) s.AddCoins(r.coins, src);
        if (r.gems > 0) s.AddGems(r.gems, src);
        if (r.cardPacks > 0) CardService.AddPacks(r.cardPacks);
        if (r.puzzlePieces > 0) PuzzleService.GrantPieces(r.puzzlePieces);
        if (r.clubPoints > 0) ClubService.AddPoints(r.clubPoints);
        if (r.xp > 0) s.AddXp(r.xp);

        Granted?.Invoke(r, headline);
        Notifications.Bump();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Granted = null;
    }
}
