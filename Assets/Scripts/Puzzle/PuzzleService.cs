using System.Collections.Generic;
using UnityEngine;

// Collect-the-pieces mosaic. Pieces come from quests, card sets and the wheel;
// filling the board pays a jackpot and advances to the next picture.
public static class PuzzleService
{
    public static void Init()
    {
        var s = GameState.I;
        if (s == null) return;
        if (s.Data.currentPuzzle < 0) s.Data.currentPuzzle = 0;
    }

    public static bool AllComplete =>
        GameState.I != null && GameState.I.Data.currentPuzzle >= Content.Puzzles.Length;

    public static PuzzleDef Current
    {
        get
        {
            var s = GameState.I;
            if (s == null) return Content.Puzzles[0];
            int i = Mathf.Clamp(s.Data.currentPuzzle, 0, Content.Puzzles.Length - 1);
            return Content.Puzzles[i];
        }
    }

    public static int TotalPieces => Current.PieceCount;

    public static bool HasPiece(int index)
    {
        var s = GameState.I;
        if (s == null || index < 0 || index >= 32) return false;
        return (s.Data.pieceMask & (1 << index)) != 0;
    }

    public static int OwnedCount
    {
        get
        {
            var s = GameState.I;
            if (s == null) return 0;
            int mask = s.Data.pieceMask;
            int n = 0;
            for (int i = 0; i < TotalPieces; i++)
                if ((mask & (1 << i)) != 0) n++;
            return n;
        }
    }

    public static bool IsComplete => !AllComplete && OwnedCount >= TotalPieces;

    public static int Banked => GameState.I != null ? GameState.I.Data.bankedPieces : 0;

    /// Always fills a slot the player does not have yet. Never handing out a
    /// duplicate removes the biggest frustration in this genre and costs one
    /// filtered list.
    public static void GrantPieces(int count)
    {
        var s = GameState.I;
        if (s == null || count <= 0) return;

        if (AllComplete) return;

        int granted = 0;
        var missing = new List<int>();

        while (count > 0)
        {
            if (IsComplete)
            {
                // Board is full and waiting to be claimed; hold the rest back
                // rather than throwing away a reward the player earned.
                s.Data.bankedPieces += count;
                break;
            }

            missing.Clear();
            int total = TotalPieces;
            for (int i = 0; i < total; i++)
                if (!HasPiece(i)) missing.Add(i);

            if (missing.Count == 0)
            {
                s.Data.bankedPieces += count;
                break;
            }

            int pick = missing[Random.Range(0, missing.Count)];
            s.Data.pieceMask |= 1 << pick;
            count--;
            granted++;
        }

        s.Save();
        for (int i = 0; i < granted; i++)
            GameEvents.Raise(GameEventType.PuzzlePieceEarned, 1);
        Notifications.Bump();
    }

    public static bool ClaimComplete()
    {
        var s = GameState.I;
        if (s == null || !IsComplete) return false;

        var def = Current;
        s.Data.completedPuzzles.Add(def.id);
        s.Data.currentPuzzle++;
        s.Data.pieceMask = 0;
        s.Save();

        // Granted after the advance so any pieces inside the reward land on the
        // next board rather than the one just finished.
        RewardService.Grant(def.reward, RewardSource.Puzzle, def.title + " COMPLETE!");
        GameEvents.Raise(GameEventType.PuzzleCompleted, 1);

        int banked = s.Data.bankedPieces;
        s.Data.bankedPieces = 0;
        if (banked > 0) GrantPieces(banked);

        return true;
    }
}
