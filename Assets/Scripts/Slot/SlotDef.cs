using System;
using System.Collections.Generic;
using UnityEngine;

public enum SymbolId
{
    SevenRed = 0,   // Highest classic payout (500x)
    SevenGold = 1,  // Premium gold 7 (250x)
    Bar = 2,        // Triple Bar (100x)
    Ace = 3,        // Royal Ace (50x)
    King = 4,       // Royal King (30x)
    Queen = 5,      // Royal Queen (20x)
    Jack = 6,       // Royal Jack (15x)
    Ten = 7,        // Royal 10 (10x)
    Wild = 8,       // Wild (substitutes all except Scatter, 300x)
    Scatter = 9,    // Bonus Orb (3 triggers 10 Free Spins)
    AnySeven = 10   // Special mixed 7 combo (50x)
}

[Serializable]
public struct PaylineCoord
{
    public int col;
    public int row;
    public PaylineCoord(int c, int r) { col = c; row = r; }
}

public struct WinLineResult
{
    public int lineIndex;
    public SymbolId symbol;
    public int multiplier;
    public long winAmount;
    public PaylineCoord[] coords;
}

public enum WinCelebrationTier
{
    None,
    Normal,
    BigWin,     // >= 10x
    MegaWin,    // >= 25x
    EpicWin     // >= 50x
}

public struct SpinEvaluationResult
{
    public long totalWin;
    public List<WinLineResult> winningLines;
    public bool isFreeSpinsTriggered;
    public int freeSpinsAwarded;
    public WinCelebrationTier tier;
    public float totalMultiplier;
}

public static class SlotDef
{
    public const int Cols = 3;
    public const int Rows = 3;
    public const int PaylineCount = 5;

    // Available bets in the ladder
    public static readonly long[] BetLadder =
    {
        1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000
    };

    // 5 Paylines for 3x3 layout
    public static readonly PaylineCoord[][] Paylines =
    {
        // Line 0: Middle horizontal
        new[] { new PaylineCoord(0, 1), new PaylineCoord(1, 1), new PaylineCoord(2, 1) },
        // Line 1: Top horizontal
        new[] { new PaylineCoord(0, 0), new PaylineCoord(1, 0), new PaylineCoord(2, 0) },
        // Line 2: Bottom horizontal
        new[] { new PaylineCoord(0, 2), new PaylineCoord(1, 2), new PaylineCoord(2, 2) },
        // Line 3: Diagonal down
        new[] { new PaylineCoord(0, 0), new PaylineCoord(1, 1), new PaylineCoord(2, 2) },
        // Line 4: Diagonal up
        new[] { new PaylineCoord(0, 2), new PaylineCoord(1, 1), new PaylineCoord(2, 0) }
    };

    // Line bet multipliers for 3-of-a-kind
    public static int Get3OfAKindMultiplier(SymbolId sym)
    {
        return sym switch
        {
            SymbolId.SevenRed  => 500,
            SymbolId.SevenGold => 250,
            SymbolId.Wild      => 300,
            SymbolId.Bar       => 100,
            SymbolId.Ace       => 50,
            SymbolId.King      => 30,
            SymbolId.Queen     => 20,
            SymbolId.Jack      => 15,
            SymbolId.Ten       => 10,
            _ => 0
        };
    }

    public static string GetSpritePath(SymbolId id)
    {
        return id switch
        {
            SymbolId.SevenRed  => "Assets/Art/Symbols/Sym_7_Red.png",
            SymbolId.SevenGold => "Assets/Art/Symbols/Sym_7_Gold.png",
            SymbolId.Bar       => "Assets/Art/Symbols/Sym_Bar.png",
            SymbolId.Wild      => "Assets/Art/Symbols/Sym_Wild.png",
            SymbolId.Scatter   => "Assets/Art/Symbols/Sym_Scatter.png",
            SymbolId.Ace       => "Assets/Art/Symbols/Sym_A.png",
            SymbolId.King      => "Assets/Art/Symbols/Sym_K.png",
            SymbolId.Queen     => "Assets/Art/Symbols/Sym_Q.png",
            SymbolId.Jack      => "Assets/Art/Symbols/Sym_J.png",
            SymbolId.Ten       => "Assets/Art/Symbols/Sym_10.png",
            _                  => "Assets/Art/Symbols/Sym_10.png"
        };
    }

    // Weighted strip for realistic slot distribution
    public static readonly SymbolId[] ReelStrip =
    {
        SymbolId.Ten, SymbolId.Jack, SymbolId.Queen, SymbolId.Ten, SymbolId.King,
        SymbolId.Bar, SymbolId.Ace, SymbolId.SevenGold, SymbolId.Jack, SymbolId.Ten,
        SymbolId.Wild, SymbolId.Queen, SymbolId.King, SymbolId.Scatter, SymbolId.Bar,
        SymbolId.SevenRed, SymbolId.Ace, SymbolId.Ten, SymbolId.Jack, SymbolId.Queen,
        SymbolId.King, SymbolId.Bar, SymbolId.SevenGold, SymbolId.Ace, SymbolId.Scatter,
        SymbolId.Ten, SymbolId.Wild, SymbolId.Jack, SymbolId.Queen, SymbolId.SevenRed
    };
}
