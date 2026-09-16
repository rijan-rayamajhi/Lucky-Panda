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
    Panda = 15,     // Triple Diamond high symbol / Ultra Panda WILD
    AnyBar = 16,    // Triple Diamond mixed-bar combo (result symbol only, never on a reel)

    // Ultra Panda fruits (pay-anywhere). Art is currently mapped to reused
    // placeholder symbols in SlotCatalog — swap symbolArt for real fruit art.
    Cherry = 17,
    Lemon = 18,
    Orange = 19,
    Plum = 20,
    Grape = 21,
    Watermelon = 22,
    Pineapple = 23,

    // Dragon Gold (Hold & Win). Base symbols pay via anywhere-count; Coin is
    // the collector that triggers the coin respin bonus (it never pays as a
    // normal symbol). Art is placeholder-mapped in SlotCatalog until real
    // Dragon Gold art lands.
    Dragon = 24,
    Tiger = 25,
    Koi = 26,
    Lantern = 27,
    Ingot = 28,
    Coin = 29
}

/// How a machine scores a stopped grid. Paylines walks fixed coord lines;
/// AnywhereCount pays any symbol that appears at least anywhereMinCount times
/// anywhere in the grid, and drives the cascade loop.
public enum PayMode { Paylines, AnywhereCount, HoldAndWin }

/// Count tier for AnywhereCount pay: at `minCount` or more matching symbols the
/// base payout is multiplied by `mult`. Tiers are stored highest-count first.
[Serializable]
public struct AnywherePayTier
{
    public int minCount;
    public float mult;
    public AnywherePayTier(int c, float m) { minCount = c; mult = m; }
}

/// Hold & Win jackpot rungs. Mini/Minor/Major can land on a coin; Grand is
/// awarded only for filling every cell.
public enum JackpotTier { None, Mini, Minor, Major, Grand }

/// One Hold & Win coin face: a cash value, or a fixed jackpot. `value` already
/// holds the cash for a jackpot face too, so summing faces gives the win.
public struct CoinFace
{
    public long value;
    public JackpotTier jackpot;   // None for a plain cash coin
    public CoinFace(long v, JackpotTier j = JackpotTier.None) { value = v; jackpot = j; }
}

/// Weighted cash face for the Hold & Win coin draw: pays `credits` * betUnit.
[Serializable]
public struct WeightedCoin
{
    public int credits;
    public float weight;
    public WeightedCoin(int c, float w) { credits = c; weight = w; }
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
    TripleDiamond = 1,
    LuckyPanda = 2,
    DragonGold = 3
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

    // ---- pay mode ------------------------------------------------------
    public PayMode payMode = PayMode.Paylines;
    /// AnywhereCount: fewest matching symbols anywhere in the grid that pays.
    public int anywhereMinCount = 6;
    /// AnywhereCount win unit: betUnit = totalBet / this. A 21-cell field with
    /// ~20 usable positions makes 20 the natural analog of a 20-line game; it is
    /// also the single global scale knob the balance sim tunes RTP with.
    public int anywhereBetDivisor = 20;
    /// AnywhereCount count tiers (highest count first): base payout * mult.
    public AnywherePayTier[] anywherePayTiers;

    // ---- cascade -------------------------------------------------------
    /// Chain multiplier per cascade depth; ladder[0] MUST be 1 (the first,
    /// un-cascaded evaluation). Later depths clamp to the last entry.
    public int[] cascadeMultiplierLadder;
    /// Hard cap on cascade depth — the guard against a chain that never settles.
    public int maxCascades = 12;
    /// False hides the progressive jackpot ticker row (a 3-reel convention) and
    /// frees that strip for the cascade multiplier display.
    public bool showJackpotRow = true;

    // ---- Hold & Win (coin respin / link) -------------------------------
    /// Collector coin. Landing coinsToTriggerHold of them anywhere starts the
    /// respin bonus; the coin never pays as a normal symbol (no payouts entry).
    public SymbolId coinSymbol = SymbolId.Coin;
    public int coinsToTriggerHold = 6;
    public int holdRespins = 3;
    /// Per-empty-cell chance a coin lands, on a base spin and on a respin.
    /// These plus coinValueTable are the Hold & Win RTP knobs the balance sim
    /// tunes. Coin cash uses betUnit = totalBet / anywhereBetDivisor.
    public float coinBaseLandChance = 0.125f;
    public float coinRespinLandChance = 0.11f;
    public WeightedCoin[] coinValueTable;
    /// Chance a landed coin is a fixed jackpot instead of cash, and the split
    /// among Mini/Minor/Major (Major is the remainder). Grand is board-full only.
    public float jackpotCoinChance = 0.012f;
    public float jackpotMiniShare = 0.80f;
    public float jackpotMinorShare = 0.16f;
    public int miniBetMultiplier = 15;
    public int minorBetMultiplier = 60;
    public int majorBetMultiplier = 250;
    public int grandBetMultiplier = 500;

    public int JackpotBetMultiplier(JackpotTier t) => t switch
    {
        JackpotTier.Mini => miniBetMultiplier,
        JackpotTier.Minor => minorBetMultiplier,
        JackpotTier.Major => majorBetMultiplier,
        JackpotTier.Grand => grandBetMultiplier,
        _ => 0
    };

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

    /// Tier multiplier for `count` matching symbols; 0 when below the smallest
    /// tier (i.e. below anywhereMinCount).
    public float AnywhereTierMult(int count)
    {
        if (anywherePayTiers == null) return 1f;
        for (int i = 0; i < anywherePayTiers.Length; i++)
            if (count >= anywherePayTiers[i].minCount) return anywherePayTiers[i].mult;
        return 0f;
    }

    /// Chain multiplier at cascade depth `step` (0 = first evaluation).
    public int CascadeMult(int step)
    {
        if (cascadeMultiplierLadder == null || cascadeMultiplierLadder.Length == 0) return 1;
        int i = step < 0 ? 0 : (step >= cascadeMultiplierLadder.Length ? cascadeMultiplierLadder.Length - 1 : step);
        return cascadeMultiplierLadder[i];
    }

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

    // Ultra Panda: Fruit Fortune — a wide 7x3 pay-anywhere cascade machine.
    // PLACEHOLDER ART: background/card reuse Triple Diamond art, the frame
    // overlay is omitted (no wide-window frame exists yet), and each fruit maps
    // to an existing distinct symbol sprite. Replace backgroundPath, cardPath,
    // framePath and symbolArt with real Fruit Fortune art, then Build Everything.
    public static readonly SlotGameDef LuckyPanda = new SlotGameDef
    {
        id = SlotGameId.LuckyPanda,
        displayName = "ULTRA PANDA",
        sceneName = "LuckyPanda",

        cols = 7,
        rows = 3,
        paylines = null,            // pay-anywhere: no lines
        betLadder = StandardBetLadder,

        payMode = PayMode.AnywhereCount,
        anywhereMinCount = 6,
        anywhereBetDivisor = 8,   // tuned by Verify Slot Math to ~815% return
        // 6-7 small, 8-9 medium, 10+ large. Tuned globally via anywhereBetDivisor.
        anywherePayTiers = new[]
        {
            new AnywherePayTier(10, 3.0f),
            new AnywherePayTier(8,  1.6f),
            new AnywherePayTier(6,  1.0f),
        },
        cascadeMultiplierLadder = new[] { 1, 2, 3, 5, 10 },
        maxCascades = 12,
        showJackpotRow = false,

        // Base payout per fruit at the 6+ tier. Common fruits pay little; rare
        // ones pay more. The balance sim (Verify Slot Math) tunes RTP.
        payouts = new Dictionary<SymbolId, int>
        {
            { SymbolId.Pineapple,  30 },
            { SymbolId.Watermelon, 20 },
            { SymbolId.Grape,      12 },
            { SymbolId.Plum,        8 },
            { SymbolId.Orange,      6 },
            { SymbolId.Lemon,       5 },
            { SymbolId.Cherry,      5 },
        },

        wildSymbol = SymbolId.Panda,   // substitutes for any fruit
        wildLineMultiplier = 1,
        anyComboMultiplier = 0,

        scatterSymbol = SymbolId.Scatter,
        scatterCountForFreeSpins = 3,
        freeSpinsAwarded = 10,
        scatterBetMultiplier = 5,
        freeSpinWinMultiplier = 2,

        guaranteedWinChance = 0f,      // cascades + wild make natural wins frequent
        winFavorTable = null,

        // 30 stops, no adjacent duplicates. Higher-paying fruits are rarer;
        // Panda (wild) and Scatter are the rarest.
        reelStrip = new[]
        {
            SymbolId.Cherry, SymbolId.Lemon, SymbolId.Orange, SymbolId.Cherry, SymbolId.Grape,
            SymbolId.Lemon, SymbolId.Plum, SymbolId.Cherry, SymbolId.Orange, SymbolId.Watermelon,
            SymbolId.Lemon, SymbolId.Grape, SymbolId.Cherry, SymbolId.Panda, SymbolId.Orange,
            SymbolId.Plum, SymbolId.Lemon, SymbolId.Pineapple, SymbolId.Cherry, SymbolId.Grape,
            SymbolId.Orange, SymbolId.Lemon, SymbolId.Plum, SymbolId.Scatter, SymbolId.Cherry,
            SymbolId.Watermelon, SymbolId.Orange, SymbolId.Lemon, SymbolId.Grape, SymbolId.Plum
        },

        backgroundPath = BG + "Bg_FruitFortune.png",
        framePath = UI + "Frame_FruitFortune.png",
        cardPath = UI + "Card_FruitFortune.png",
        cardHasBakedFrame = false,
        symbolArt = new Dictionary<SymbolId, string>
        {
            { SymbolId.Cherry,     Sym + "Sym_Cherry.png" },
            { SymbolId.Lemon,      Sym + "Sym_Lemon.png" },
            { SymbolId.Orange,     Sym + "Sym_Orange.png" },
            { SymbolId.Plum,       Sym + "Sym_Plum.png" },
            { SymbolId.Grape,      Sym + "Sym_Grapes.png" },
            { SymbolId.Watermelon, Sym + "Sym_Watermelon.png" },
            { SymbolId.Pineapple,  Sym + "Sym_Pineapple.png" },
            { SymbolId.Panda,      Sym + "Sym_PandaWild.png" },
            { SymbolId.Scatter,    Sym + "Sym_Scatter.png" },  // reused bonus orb
        },

        // Frame_FruitFortune is 1561x1008; its transparent opening (flood-filled)
        // is 79.4% W x 52.7% H, centred 57px (image space) below the frame centre
        // — taller at the sides than the crest-notched middle. At a 1240x800
        // cabinet that is a ~984x422 opening centred at y=-46. The velvet backdrop
        // is sized past it (1020x450) so it fills to the gold border with no scene
        // showing through top/bottom; the frame overlay hides the overhang.
        cabinetSize = new Vector2(1240, 800),
        backdropSize = new Vector2(1020, 450),
        reelWindowSize = new Vector2(986, 392),
        reelWindowY = -46f,
        // Grid fills the 986x392 window with even margins: column pitch 138 puts
        // the outer symbols at +/-414 (edge +/-474, ~19px clear of the 493-half
        // window); row pitch 128 gives ~8px top/bottom. Verified against the
        // frame art with a composite render, so symbols fill without kissing the
        // gold border.
        colWidth = 130f,
        colSpacing = 8f,
        rowHeight = 128f,
        symbolSize = new Vector2(120, 120),
        dividerHeight = 372f,
    };

    // Dragon Gold: Ultra Panda — a 5x3 Hold & Win (coin respin / link) machine.
    // Base game pays via anywhere-count (like Ultra Panda but no cascade); the
    // marquee feature is the coin bonus: land 6+ Dragon Coins to lock them and
    // respin for coin values and Mini/Minor/Major/Grand jackpots.
    // Art is bespoke Dragon Gold (dragon/tiger/koi/lantern/ingot symbols, a
    // gold-coin collector, a twin-dragon 5x3 frame). RTP/jackpot balance is
    // tuned by Verify Slot Math via anywhereBetDivisor, payouts, coin land
    // chances and the coin/jackpot value tables (sim ~817% return, bonus ~1 in
    // 140 spins).
    public static readonly SlotGameDef DragonGold = new SlotGameDef
    {
        id = SlotGameId.DragonGold,
        displayName = "DRAGON GOLD",
        sceneName = "DragonGold",

        cols = 5,
        rows = 3,
        paylines = null,            // anywhere-count base, no lines
        betLadder = StandardBetLadder,

        payMode = PayMode.HoldAndWin,
        anywhereMinCount = 6,
        anywhereBetDivisor = 1,     // base pay unit = total bet (tuned to ~764% base)
        anywherePayTiers = new[]
        {
            new AnywherePayTier(10, 3.0f),
            new AnywherePayTier(8,  1.6f),
            new AnywherePayTier(6,  1.0f),
        },
        // No cascade: base is a single evaluation. Ladder still starts at 1 so
        // the math check's "ladder[0] must be 1" rule holds.
        cascadeMultiplierLadder = new[] { 1 },
        maxCascades = 1,
        showJackpotRow = false,     // no ticker row — the dragon frame stands alone

        // Base payout per symbol at the 6+ tier. Commons pay little, Dragon most.
        payouts = new Dictionary<SymbolId, int>
        {
            { SymbolId.Dragon,  74 },
            { SymbolId.Tiger,   49 },
            { SymbolId.Koi,     29 },
            { SymbolId.Lantern, 20 },
            { SymbolId.Ingot,   15 },
        },

        wildSymbol = SymbolId.Wild,   // substitutes for any base symbol
        wildLineMultiplier = 1,
        anyComboMultiplier = 0,

        // No scatter free-spins feature — the coin bonus is the feature. Scatter
        // is not on the strip, so it never triggers.
        scatterSymbol = SymbolId.Scatter,
        scatterCountForFreeSpins = 99,
        freeSpinsAwarded = 0,
        scatterBetMultiplier = 0,
        freeSpinWinMultiplier = 1,

        guaranteedWinChance = 0f,
        winFavorTable = null,

        // ---- Hold & Win config ----
        coinSymbol = SymbolId.Coin,
        coinsToTriggerHold = 6,
        holdRespins = 3,
        coinBaseLandChance = 0.125f,
        coinRespinLandChance = 0.11f,
        coinValueTable = new[]
        {
            new WeightedCoin(1,  44f),
            new WeightedCoin(2,  28f),
            new WeightedCoin(3,  14f),
            new WeightedCoin(5,   8f),
            new WeightedCoin(8,   4f),
            new WeightedCoin(15,  2f),
            new WeightedCoin(40,  1f),
        },
        jackpotCoinChance = 0.012f,
        jackpotMiniShare = 0.80f,
        jackpotMinorShare = 0.16f,
        miniBetMultiplier = 15,
        minorBetMultiplier = 60,
        majorBetMultiplier = 250,
        grandBetMultiplier = 500,

        // 30 stops, no adjacent duplicates. Commons (Ingot/Lantern) dense so the
        // 6+ anywhere tiers fire; Dragon and Wild rare. Coin is NOT on the strip
        // — coins are sprinkled by coinBaseLandChance in GenerateOutcome.
        reelStrip = new[]
        {
            SymbolId.Ingot, SymbolId.Lantern, SymbolId.Koi, SymbolId.Ingot, SymbolId.Tiger,
            SymbolId.Lantern, SymbolId.Dragon, SymbolId.Ingot, SymbolId.Koi, SymbolId.Lantern,
            SymbolId.Ingot, SymbolId.Wild, SymbolId.Koi, SymbolId.Tiger, SymbolId.Ingot,
            SymbolId.Lantern, SymbolId.Dragon, SymbolId.Koi, SymbolId.Ingot, SymbolId.Lantern,
            SymbolId.Tiger, SymbolId.Ingot, SymbolId.Koi, SymbolId.Wild, SymbolId.Lantern,
            SymbolId.Dragon, SymbolId.Ingot, SymbolId.Tiger, SymbolId.Lantern, SymbolId.Koi
        },

        backgroundPath = BG + "Bg_DragonGold.png",
        framePath = UI + "Frame_DragonGold.png",
        cardPath = UI + "Card_DragonGold.png",
        cardHasBakedFrame = false,
        symbolArt = new Dictionary<SymbolId, string>
        {
            { SymbolId.Dragon,  Sym + "Sym_Dragon.png" },
            { SymbolId.Tiger,   Sym + "Sym_Tiger.png" },
            { SymbolId.Koi,     Sym + "Sym_Koi.png" },
            { SymbolId.Lantern, Sym + "Sym_Lantern.png" },
            { SymbolId.Ingot,   Sym + "Sym_Ingot.png" },
            { SymbolId.Wild,    Sym + "Sym_Wild.png" },   // shared wild sprite
            { SymbolId.Coin,    Sym + "Sym_DragonCoin.png" },  // flat inner ring holds the value
        },

        // Frame_DragonGold is 1536x1024 (aspect 1.5); its transparent opening
        // measures 67.3% W x 43.8% H, centred, sitting 36px (image space) below
        // the frame centre. At a 1200x800 cabinet that is an ~806x350 window at
        // y=-28. The velvet backdrop is sized past it so it fills to the gold
        // border with no scene showing through; the frame overlay hides the
        // overhang. Grid: pitch 161 puts outer columns at +/-322 (~21px clear of
        // the 403-half window); row pitch 116 gives ~9px top/bottom clearance.
        cabinetSize = new Vector2(1200, 800),
        backdropSize = new Vector2(846, 396),
        reelWindowSize = new Vector2(806, 350),
        reelWindowY = -28f,
        colWidth = 150f,
        colSpacing = 11f,
        rowHeight = 106f,
        symbolSize = new Vector2(120, 120),
        dividerHeight = 330f,
    };

    public static readonly SlotGameDef[] All = { Classic777, TripleDiamond, LuckyPanda, DragonGold };

    public static SlotGameDef Get(SlotGameId id)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return Classic777;
    }
}
