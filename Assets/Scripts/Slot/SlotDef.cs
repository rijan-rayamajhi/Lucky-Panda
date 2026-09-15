using System;
using System.Collections.Generic;
using UnityEngine;

// Enum values are serialized into the scenes (SlotMachine.symbolEntries), so
// existing ids must keep their numbers. New symbols append.
public enum SymbolId
{
    SevenRed = 0,   // Classic 777: highest classic payout
    SevenGold = 1,  // Classic 777: premium gold 7
    Bar = 2,        // Classic 777: triple bar
    Ace = 3,
    King = 4,
    Queen = 5,
    Jack = 6,
    Ten = 7,
    Wild = 8,       // Classic 777 wild
    Scatter = 9,    // Shared bonus orb
    AnySeven = 10,  // Classic 777 mixed-seven combo (result symbol only, never on a reel)

    Diamond = 11,   // Triple Diamond wild — multiplies the line it substitutes into
    Bar1 = 12,      // Single BAR
    Bar2 = 13,      // Double BAR
    Bar3 = 14,      // Triple BAR
    Panda = 15,     // Triple Diamond high symbol
    AnyBar = 16     // Triple Diamond mixed-bar combo (result symbol only, never on a reel)
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
    /// How much the substituting wilds multiplied this line (1 = none).
    public int wildMultiplier;
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

public struct WeightedSymbol
{
    public SymbolId symbol;
    public float weight;
    public WeightedSymbol(SymbolId s, float w) { symbol = s; weight = w; }
}

public enum SlotGameId
{
    Classic777 = 0,
    TripleDiamond = 1
}

/// Everything that makes one slot machine different from another. The machine,
/// evaluator, reels and scene builder all read from this rather than from
/// compile-time constants, so a new game is a new entry in SlotCatalog.
public class SlotGameDef
{
    public SlotGameId id;
    public string displayName;
    public string sceneName;
    public string ScenePath => "Assets/Scenes/" + sceneName + ".unity";

    // ---- grid ----------------------------------------------------------
    public int cols = 3;
    public int rows = 3;
    public PaylineCoord[][] paylines;
    public long[] betLadder;
    public SymbolId[] reelStrip;
    public int PaylineCount => paylines.Length;

    // ---- paytable ------------------------------------------------------
    public Dictionary<SymbolId, int> payouts;

    public SymbolId wildSymbol;
    /// Each wild that SUBSTITUTES into a win multiplies that line's payout by
    /// this. 1 disables it. Triple Diamond uses 3, so two diamonds pay 9x.
    /// A line made entirely of wilds pays its own flat payout instead.
    public int wildLineMultiplier = 1;

    /// Mixed combo: any three of these (wilds count) pay anyComboMultiplier.
    public SymbolId[] anyComboMembers;
    public SymbolId anyComboSymbol;
    public int anyComboMultiplier;

    public SymbolId scatterSymbol = SymbolId.Scatter;
    public int scatterCountForFreeSpins = 3;
    public int freeSpinsAwarded = 10;
    public int scatterBetMultiplier = 5;
    public int freeSpinWinMultiplier = 2;

    /// Chance a spin is nudged into a guaranteed line win, and the weighted
    /// pool that win is drawn from. Top-award symbols are deliberately absent
    /// from the pool — those only land naturally off the reel strip.
    public float guaranteedWinChance = 0.35f;
    public WeightedSymbol[] winFavorTable;

    // ---- art -----------------------------------------------------------
    public string backgroundPath;
    public string framePath;
    public string cardPath;
    /// True when the lobby tile art already has a border painted in, so the
    /// universal Frame_SlotGame overlay must be skipped to avoid a double frame.
    public bool cardHasBakedFrame;
    public Dictionary<SymbolId, string> symbolArt;

    // ---- cabinet layout, in canvas px ----------------------------------
    public Vector2 cabinetSize;
    public Vector2 backdropSize;
    public Vector2 reelWindowSize;
    public float reelWindowY;
    public float colWidth;
    public float colSpacing;
    public float rowHeight;
    public Vector2 symbolSize;
    public float dividerHeight;

    public int PayoutFor(SymbolId s) =>
        payouts != null && payouts.TryGetValue(s, out var m) ? m : 0;

    public bool IsAnyComboMember(SymbolId s)
    {
        if (s == wildSymbol) return true;
        if (anyComboMembers == null) return false;
        for (int i = 0; i < anyComboMembers.Length; i++)
            if (anyComboMembers[i] == s) return true;
        return false;
    }

    public string ArtFor(SymbolId s) =>
        symbolArt != null && symbolArt.TryGetValue(s, out var p) ? p : null;
}

public static class SlotCatalog
{
    const string Sym = "Assets/Art/Symbols/";
    const string UI = "Assets/Art/UI/";
    const string BG = "Assets/Art/Backgrounds/";

    // 5 paylines on a 3x3 grid — shared by both machines.
    static PaylineCoord[][] Grid3x3Paylines => new[]
    {
        // Line 0: middle horizontal
        new[] { new PaylineCoord(0, 1), new PaylineCoord(1, 1), new PaylineCoord(2, 1) },
        // Line 1: top horizontal
        new[] { new PaylineCoord(0, 0), new PaylineCoord(1, 0), new PaylineCoord(2, 0) },
        // Line 2: bottom horizontal
        new[] { new PaylineCoord(0, 2), new PaylineCoord(1, 2), new PaylineCoord(2, 2) },
        // Line 3: diagonal down
        new[] { new PaylineCoord(0, 0), new PaylineCoord(1, 1), new PaylineCoord(2, 2) },
        // Line 4: diagonal up
        new[] { new PaylineCoord(0, 2), new PaylineCoord(1, 1), new PaylineCoord(2, 0) }
    };

    static readonly long[] StandardBetLadder =
    {
        1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000
    };

    public static readonly SlotGameDef Classic777 = new SlotGameDef
    {
        id = SlotGameId.Classic777,
        displayName = "CLASSIC 777",
        sceneName = "SlotGame",

        cols = 3,
        rows = 3,
        paylines = Grid3x3Paylines,
        betLadder = StandardBetLadder,

        payouts = new Dictionary<SymbolId, int>
        {
            { SymbolId.SevenRed,  500 },
            { SymbolId.SevenGold, 250 },
            { SymbolId.Wild,      300 },
            { SymbolId.Bar,       100 },
            { SymbolId.Ace,        50 },
            { SymbolId.King,       30 },
            { SymbolId.Queen,      20 },
            { SymbolId.Jack,       15 },
            { SymbolId.Ten,        10 },
        },

        wildSymbol = SymbolId.Wild,
        wildLineMultiplier = 1,
        anyComboMembers = new[] { SymbolId.SevenRed, SymbolId.SevenGold },
        anyComboSymbol = SymbolId.AnySeven,
        anyComboMultiplier = 50,

        winFavorTable = new[]
        {
            new WeightedSymbol(SymbolId.SevenRed,  0.05f),
            new WeightedSymbol(SymbolId.SevenGold, 0.07f),
            new WeightedSymbol(SymbolId.Bar,       0.13f),
            new WeightedSymbol(SymbolId.Ace,       0.20f),
            new WeightedSymbol(SymbolId.King,      0.25f),
            new WeightedSymbol(SymbolId.Queen,     0.30f),
        },

        reelStrip = new[]
        {
            SymbolId.Ten, SymbolId.Jack, SymbolId.Queen, SymbolId.Ten, SymbolId.King,
            SymbolId.Bar, SymbolId.Ace, SymbolId.SevenGold, SymbolId.Jack, SymbolId.Ten,
            SymbolId.Wild, SymbolId.Queen, SymbolId.King, SymbolId.Scatter, SymbolId.Bar,
            SymbolId.SevenRed, SymbolId.Ace, SymbolId.Ten, SymbolId.Jack, SymbolId.Queen,
            SymbolId.King, SymbolId.Bar, SymbolId.SevenGold, SymbolId.Ace, SymbolId.Scatter,
            SymbolId.Ten, SymbolId.Wild, SymbolId.Jack, SymbolId.Queen, SymbolId.SevenRed
        },

        backgroundPath = BG + "Bg_Slot777.png",
        framePath = UI + "Frame_Classic777.png",
        cardPath = UI + "Card_Slot777.png",
        cardHasBakedFrame = true,
        symbolArt = new Dictionary<SymbolId, string>
        {
            { SymbolId.SevenRed,  Sym + "Sym_7_Red.png" },
            { SymbolId.SevenGold, Sym + "Sym_7_Gold.png" },
            { SymbolId.Bar,       Sym + "Sym_Bar.png" },
            { SymbolId.Wild,      Sym + "Sym_Wild.png" },
            { SymbolId.Scatter,   Sym + "Sym_Scatter.png" },
            { SymbolId.Ace,       Sym + "Sym_A.png" },
            { SymbolId.King,      Sym + "Sym_K.png" },
            { SymbolId.Queen,     Sym + "Sym_Q.png" },
            { SymbolId.Jack,      Sym + "Sym_J.png" },
            { SymbolId.Ten,       Sym + "Sym_10.png" },
        },

        // Frame_Classic777 is 1448x1086; inner window measures 74.0% W, 45.9% H
        // sitting 24px (image space) below centre.
        cabinetSize = new Vector2(920, 690),
        backdropSize = new Vector2(676, 314),
        reelWindowSize = new Vector2(670, 310),
        reelWindowY = -16f,
        colWidth = 214f,
        colSpacing = 12f,
        rowHeight = 102f,
        symbolSize = new Vector2(190, 100),
        dividerHeight = 300f,
    };

    public static readonly SlotGameDef TripleDiamond = new SlotGameDef
    {
        id = SlotGameId.TripleDiamond,
        displayName = "TRIPLE DIAMOND",
        sceneName = "TripleDiamond",

        cols = 3,
        rows = 3,
        paylines = Grid3x3Paylines,
        betLadder = StandardBetLadder,

        // Tuned by simulation to 812% return over 2M spins, matching Classic
        // 777's 813% — bars are common so the mixed-bar combo fires often for
        // a little, which is why these sit below the 777 numbers. The 1000x
        // diamond costs only ~2 points of that: it lands 1 spin in 6,500.
        payouts = new Dictionary<SymbolId, int>
        {
            { SymbolId.Diamond,  1000 },
            { SymbolId.SevenRed,  200 },
            { SymbolId.SevenGold, 120 },
            { SymbolId.Panda,      70 },
            { SymbolId.Bar3,       45 },
            { SymbolId.Bar2,       20 },
            { SymbolId.Bar1,       10 },
        },

        wildSymbol = SymbolId.Diamond,
        wildLineMultiplier = 3,
        anyComboMembers = new[] { SymbolId.Bar1, SymbolId.Bar2, SymbolId.Bar3 },
        anyComboSymbol = SymbolId.AnyBar,
        anyComboMultiplier = 5,

        // Diamond is absent on purpose: a forced 1000x line would fire on ~1%
        // of spins. Diamonds only arrive naturally off the strip.
        winFavorTable = new[]
        {
            new WeightedSymbol(SymbolId.SevenRed,  0.05f),
            new WeightedSymbol(SymbolId.SevenGold, 0.08f),
            new WeightedSymbol(SymbolId.Panda,     0.17f),
            new WeightedSymbol(SymbolId.Bar3,      0.20f),
            new WeightedSymbol(SymbolId.Bar2,      0.25f),
            new WeightedSymbol(SymbolId.Bar1,      0.25f),
        },

        // 30 stops, no two neighbours alike — GenerateOutcome reads three
        // consecutive stops per column, so runs would manufacture free wins.
        reelStrip = new[]
        {
            SymbolId.Bar1, SymbolId.Bar2, SymbolId.Panda, SymbolId.Bar3, SymbolId.Bar1,
            SymbolId.SevenGold, SymbolId.Bar2, SymbolId.Diamond, SymbolId.Bar1, SymbolId.Bar3,
            SymbolId.Panda, SymbolId.Bar2, SymbolId.SevenRed, SymbolId.Bar1, SymbolId.Scatter,
            SymbolId.Bar3, SymbolId.Bar2, SymbolId.Panda, SymbolId.Bar1, SymbolId.SevenGold,
            SymbolId.Bar3, SymbolId.Bar2, SymbolId.Scatter, SymbolId.Bar1, SymbolId.Panda,
            SymbolId.SevenRed, SymbolId.Bar3, SymbolId.Bar1, SymbolId.SevenGold, SymbolId.Bar2
        },

        backgroundPath = BG + "Bg_TripleDiamond.png",
        framePath = UI + "Frame_TripleDiamond.png",
        cardPath = UI + "Card_TripleDiamond.png",
        cardHasBakedFrame = false,
        symbolArt = new Dictionary<SymbolId, string>
        {
            { SymbolId.Diamond,   Sym + "Sym_Diamond.png" },
            { SymbolId.SevenRed,  Sym + "Sym_7_Red.png" },
            { SymbolId.SevenGold, Sym + "Sym_7_Gold.png" },
            { SymbolId.Panda,     Sym + "Sym_Panda.png" },
            { SymbolId.Bar3,      Sym + "Sym_Bar3.png" },
            { SymbolId.Bar2,      Sym + "Sym_Bar2.png" },
            { SymbolId.Bar1,      Sym + "Sym_Bar1.png" },
            { SymbolId.Scatter,   Sym + "Sym_Scatter.png" },
        },

        // Frame_TripleDiamond is 1446x1087; inner window measures 71.0% W,
        // 49.0% H sitting 82px (image space) below centre — its crown header
        // is taller than the 777 cabinet's, so the reels sit lower.
        cabinetSize = new Vector2(920, 692),
        backdropSize = new Vector2(648, 335),
        reelWindowSize = new Vector2(642, 331),
        reelWindowY = -52f,
        colWidth = 206f,
        colSpacing = 12f,
        rowHeight = 109f,
        symbolSize = new Vector2(198, 105),
        dividerHeight = 320f,
    };

    public static readonly SlotGameDef[] All = { Classic777, TripleDiamond };

    public static SlotGameDef Get(SlotGameId id)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return Classic777;
    }
}
