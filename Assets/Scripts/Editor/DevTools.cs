using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Testing a 24-hour offline loop by hand is not practical, so the loops get
// shortcuts. Everything here is editor-only and never ships.
public static class DevTools
{
    const string Menu = "Lucky Panda/Dev/";

    [MenuItem(Menu + "Force Day Rollover", true)]
    [MenuItem(Menu + "Grant 5 Card Packs", true)]
    [MenuItem(Menu + "Grant 3 Puzzle Pieces", true)]
    [MenuItem(Menu + "Unlock All Cards", true)]
    [MenuItem(Menu + "Add 10,000 Club Points", true)]
    [MenuItem(Menu + "Grant 100,000 Coins", true)]
    static bool RequiresPlayMode() => Application.isPlaying && GameState.I != null;

    [MenuItem(Menu + "Force Day Rollover")]
    static void ForceRollover()
    {
        var s = GameState.I;
        s.Data.lastDayIndex -= 1;
        s.Data.questDayIndex -= 1;
        s.Data.lastWheelUtc = "";
        s.SaveNow();
        DailyService.Boot();
        Debug.Log("Day rolled over. Quests reset, wheel unlocked.");
    }

    [MenuItem(Menu + "Grant 5 Card Packs")]
    static void GrantPacks()
    {
        CardService.AddPacks(5);
        Debug.Log("Granted 5 card packs.");
    }

    [MenuItem(Menu + "Grant 3 Puzzle Pieces")]
    static void GrantPieces()
    {
        PuzzleService.GrantPieces(3);
        Debug.Log("Granted 3 puzzle pieces.");
    }

    [MenuItem(Menu + "Unlock All Cards")]
    static void UnlockCards()
    {
        var s = GameState.I;
        for (int i = 0; i < s.Data.cardCounts.Length; i++)
            if (s.Data.cardCounts[i] == 0) s.Data.cardCounts[i] = 1;
        s.SaveNow();
        s.NotifyChanged();
        Debug.Log("All cards unlocked. Every set is now claimable.");
    }

    [MenuItem(Menu + "Add 10,000 Club Points")]
    static void AddClubPoints()
    {
        ClubService.AddPoints(10_000);
        Debug.Log("Club points now " + ClubService.Points + " (" + ClubService.Current.tierName + ").");
    }

    [MenuItem(Menu + "Grant 100,000 Coins")]
    static void GrantCoins()
    {
        GameState.I.AddCoins(100_000, RewardSource.Dev);
        Debug.Log("Granted 100,000 coins.");
    }

    // ---- self-check ----------------------------------------------------

    // Not a test framework and not trying to be: one menu item that fails
    // loudly if the logic the features depend on stops holding. Runs in edit
    // mode against a throwaway GameState, so the real save is never touched.
    [MenuItem("Lucky Panda/Dev/Run Self-Checks")]
    public static void RunSelfChecks()
    {
        var log = new StringBuilder();
        int failures = 0;

        void Check(bool condition, string label)
        {
            if (!condition) failures++;
            log.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }

        // --- content integrity, no state needed ---
        Check(Content.Cards.Length == Content.CardSets.Length * Content.CardsPerSet,
            "card count matches sets x cards-per-set");

        bool namesOk = true;
        for (int i = 0; i < Content.Cards.Length; i++)
            if (string.IsNullOrEmpty(Content.Cards[i].cardName)) namesOk = false;
        Check(namesOk, "every card has a name");

        var ids = new HashSet<string>();
        bool uniqueQuests = true;
        for (int i = 0; i < Content.Quests.Length; i++)
            if (!ids.Add(Content.Quests[i].id)) uniqueQuests = false;
        Check(uniqueQuests, "quest ids are unique");

        bool piecesFit = true;
        for (int i = 0; i < Content.Puzzles.Length; i++)
            if (Content.Puzzles[i].PieceCount > 32) piecesFit = false;
        Check(piecesFit, "every puzzle fits the 32-bit piece mask");

        bool tiersRise = true;
        for (int i = 1; i < Content.ClubTiers.Length; i++)
            if (Content.ClubTiers[i].points <= Content.ClubTiers[i - 1].points) tiersRise = false;
        Check(tiersRise, "club tier thresholds strictly increase");

        // --- quest selection determinism ---
        var a = QuestService.PreviewSelection(4242, false, 3);
        var b = QuestService.PreviewSelection(4242, false, 3);
        bool same = a.Count == b.Count;
        for (int i = 0; same && i < a.Count; i++) if (a[i] != b[i]) same = false;
        Check(same, "same day index yields identical quests");

        bool slotFiltered = true;
        var pool = QuestService.PreviewSelection(1, false, 99);
        for (int i = 0; i < pool.Count; i++)
        {
            var def = Content.Quest(pool[i]);
            if (def != null && def.needsSlotGame && !Content.SlotGameAvailable) slotFiltered = false;
        }
        Check(slotFiltered, "slot quests stay out of the pool while the slot game is off");
        Check(QuestService.PoolSize(false) >= Content.QuestsPerDay,
            "enough non-slot dailies exist to fill the board");

        // --- rarity distribution ---
        const int rolls = 30000;
        var counts = new int[Content.RarityWeights.Length];
        for (int i = 0; i < rolls; i++) counts[CardService.PreviewRarityRoll()]++;
        int weightTotal = 0;
        for (int r = 1; r < Content.RarityWeights.Length; r++) weightTotal += Content.RarityWeights[r];
        bool distributionOk = true;
        for (int r = 1; r < Content.RarityWeights.Length; r++)
        {
            float expected = Content.RarityWeights[r] / (float)weightTotal;
            float actual = counts[r] / (float)rolls;
            if (Mathf.Abs(actual - expected) > 0.02f) distributionOk = false;
        }
        Check(distributionOk, "pack rarity distribution tracks the configured weights");

        // --- stateful checks against a throwaway GameState ---
        var probe = new GameObject("SelfCheckState");
        probe.hideFlags = HideFlags.HideAndDontSave;
        var previous = GameState.I;
        try
        {
            var state = probe.AddComponent<GameState>();
            GameState.I = state;
            state.Data = new PlayerData();
            CardService.Init();

            // Puzzle: pieces never duplicate, and the board terminates.
            int total = PuzzleService.TotalPieces;
            for (int i = 0; i < total; i++) PuzzleService.GrantPieces(1);
            Check(PuzzleService.OwnedCount == total, "granting N pieces fills exactly N distinct slots");
            Check(PuzzleService.IsComplete, "board reports complete when full");

            PuzzleService.GrantPieces(2);
            Check(PuzzleService.Banked == 2, "pieces earned on a full board are banked, not lost");

            // Club multiplier applies to wins and not to purchases.
            ClubService.AddPoints(Content.ClubTiers[Content.ClubTiers.Length - 1].points);
            float mult = ClubService.CoinMultiplier;
            Check(mult > 1f, "top club tier raises the coin multiplier");

            long before = state.Data.coins;
            state.AddCoins(1000, RewardSource.Win);
            long winDelta = state.Data.coins - before;

            before = state.Data.coins;
            state.AddCoins(1000, RewardSource.Shop);
            long shopDelta = state.Data.coins - before;

            Check(winDelta == (long)System.Math.Round(1000 * (double)mult),
                "club multiplier applies to a win");
            Check(shopDelta == 1000, "club multiplier does NOT apply to a shop purchase");

            // Mail cap holds.
            for (int i = 0; i < MailService.MaxMessages + 15; i++)
                MailService.Send("probe_" + i, "TEST", "TEST " + i, "body", Reward.Of(coins: 1));
            Check(MailService.All.Count <= MailService.MaxMessages,
                "inbox never exceeds its message cap");

            // A moved clock must not be able to lock the wheel for longer than a
            // normal cooldown. Clamping a future stamp to now leaves at most the
            // usual 24 hours; the bug it prevents is a multi-year countdown, not
            // the cooldown itself.
            state.Data.lastWheelUtc = System.DateTime.UtcNow.AddYears(3).ToString("o");
            DailyService.Boot();
            DailyService.WheelReady(out var wait);
            Check(wait.TotalHours <= 24.01,
                "a wheel timestamp years in the future clamps to a <=24h wait, not a multi-year lock");

            // And an ordinary expired stamp still reads as ready.
            state.Data.lastWheelUtc = System.DateTime.UtcNow.AddHours(-25).ToString("o");
            Check(DailyService.WheelReady(out _), "a spin older than 24h is available again");
        }
        finally
        {
            GameState.I = previous;
            Object.DestroyImmediate(probe);
        }

        // --- formatting ---
        Check(Reward.Of(coins: 1500, gems: 2).Describe().Contains("1,500"),
            "reward text uses invariant grouping");

        string header = failures == 0
            ? "<color=green>Self-checks passed</color>"
            : "<color=red>" + failures + " self-check(s) FAILED</color>";
        Debug.Log(header + "\n" + log);
    }
}
