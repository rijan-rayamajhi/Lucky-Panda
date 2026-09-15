using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

/// Guards the slot paytable rules, catalog integrity and payout balance.
///
/// Run it after touching SlotDef/SlotEvaluator or adding a machine. It caught
/// Triple Diamond shipping at 1757% return against Classic 777's 813%, which
/// would have made the new game strictly better to play and inflated the coin
/// economy twice as fast.
public static class SlotMathCheck
{
    static int failures;
    static StringBuilder log;

    [MenuItem("Lucky Panda/Dev/Verify Slot Math")]
    public static void Verify()
    {
        failures = 0;
        log = new StringBuilder();

        foreach (var def in SlotCatalog.All)
        {
            log.AppendLine($"--- {def.displayName} ({def.sceneName}.unity) ---");
            CheckRules(def);
            CheckCatalog(def);
        }

        CheckBalance();

        if (failures == 0) Debug.Log("Slot math OK\n" + log);
        else Debug.LogError($"Slot math: {failures} problem(s)\n" + log);
    }

    static void Fail(string msg) { failures++; log.AppendLine("  FAIL " + msg); }
    static void Pass(string msg) { log.AppendLine("  ok   " + msg); }

    static void Expect(string what, long got, long want)
    {
        if (got == want) Pass($"{what} = {want:N0}");
        else Fail($"{what}: got {got:N0}, expected {want:N0}");
    }

    // Grid is [col, row]; args fill the middle row, which is payline 0.
    static SymbolId[,] MidRow(SlotGameDef def, SymbolId a, SymbolId b, SymbolId c,
                              SymbolId f1, SymbolId f2, SymbolId f3)
    {
        var g = new SymbolId[def.cols, def.rows];
        // Filler is arranged so no other payline (rows 0/2, both diagonals)
        // can form a win, isolating the rule under test.
        g[0, 0] = f1; g[1, 0] = f2; g[2, 0] = f3;
        g[0, 1] = a;  g[1, 1] = b;  g[2, 1] = c;
        g[0, 2] = f2; g[1, 2] = f1; g[2, 2] = f3;
        return g;
    }

    static void CheckRules(SlotGameDef def)
    {
        if (def.payMode == PayMode.AnywhereCount) { CheckAnywhereRules(def); return; }

        // Filler: three symbols that pay nothing together and aren't any-combo
        // members, so only the middle row can win.
        SymbolId f1, f2, f3;
        if (def.id == SlotGameId.TripleDiamond) { f1 = SymbolId.Panda; f2 = SymbolId.SevenRed; f3 = SymbolId.SevenGold; }
        else { f1 = SymbolId.Ten; f2 = SymbolId.Jack; f3 = SymbolId.Queen; }

        long bet = 5000;
        long line = bet / def.PaylineCount;
        var wild = def.wildSymbol;
        int wm = def.wildLineMultiplier;

        // Pick the highest-paying non-wild symbol to substitute into.
        SymbolId sub = SymbolId.Ten; int subPay = 0;
        foreach (var kv in def.payouts)
            if (kv.Key != wild && kv.Value > subPay && Array.IndexOf(def.reelStrip, kv.Key) >= 0)
            { sub = kv.Key; subPay = kv.Value; }

        var r = SlotEvaluator.Evaluate(MidRow(def, wild, wild, wild, f1, f2, f3), bet, def);
        Expect($"3 wilds pay flat {def.PayoutFor(wild)}x (never self-multiplied)", r.totalWin, line * def.PayoutFor(wild));

        r = SlotEvaluator.Evaluate(MidRow(def, wild, sub, sub, f1, f2, f3), bet, def);
        Expect($"1 wild + 2 {sub} = {subPay}x * {wm}", r.totalWin, line * subPay * wm);

        r = SlotEvaluator.Evaluate(MidRow(def, wild, wild, sub, f1, f2, f3), bet, def);
        Expect($"2 wilds + 1 {sub} = {subPay}x * {wm * wm}", r.totalWin, line * subPay * wm * wm);

        if (def.anyComboMultiplier > 0 && def.anyComboMembers != null && def.anyComboMembers.Length >= 3)
        {
            var m = def.anyComboMembers;
            r = SlotEvaluator.Evaluate(MidRow(def, m[0], m[1], m[2], f1, f2, f3), bet, def);
            Expect($"mixed {def.anyComboSymbol} = {def.anyComboMultiplier}x", r.totalWin, line * def.anyComboMultiplier);
        }

        // Scatters pay from anywhere and must never count as a line win.
        var scatterGrid = new SymbolId[def.cols, def.rows];
        for (int c = 0; c < def.cols; c++)
            for (int row = 0; row < def.rows; row++)
                scatterGrid[c, row] = c == row ? def.scatterSymbol : (row == 0 ? f1 : f2);
        r = SlotEvaluator.Evaluate(scatterGrid, bet, def);
        if (!r.isFreeSpinsTriggered || r.freeSpinsAwarded != def.freeSpinsAwarded)
            Fail($"{def.scatterCountForFreeSpins} scatters should award {def.freeSpinsAwarded} free spins");
        else Pass($"scatters award {def.freeSpinsAwarded} free spins + {def.scatterBetMultiplier}x bet");
    }

    // Build a grid holding exactly `count` of `sym` plus `wildCount` wilds; the
    // rest is a spread of other paying symbols, each kept below the pay
    // threshold, so only `sym` (with the wilds substituting) can win.
    static SymbolId[,] AnywhereGrid(SlotGameDef def, SymbolId sym, int count, int wildCount)
    {
        var fillers = new List<SymbolId>();
        foreach (var kv in def.payouts) if (kv.Key != sym) fillers.Add(kv.Key);

        var g = new SymbolId[def.cols, def.rows];
        int placed = 0, placedWild = 0, fi = 0;
        for (int c = 0; c < def.cols; c++)
            for (int r = 0; r < def.rows; r++)
            {
                if (placed < count) { g[c, r] = sym; placed++; }
                else if (placedWild < wildCount) { g[c, r] = def.wildSymbol; placedWild++; }
                else { g[c, r] = fillers[fi++ % fillers.Count]; }
            }
        return g;
    }

    static void CheckAnywhereRules(SlotGameDef def)
    {
        if (def.cascadeMultiplierLadder == null || def.cascadeMultiplierLadder.Length == 0 ||
            def.cascadeMultiplierLadder[0] != 1)
            Fail("cascade ladder must start at 1 (the un-cascaded first evaluation)");
        else
            Pass($"cascade ladder [{string.Join(",", def.cascadeMultiplierLadder)}], cap {def.maxCascades}");

        const long bet = 100_000;
        long unit = bet / def.anywhereBetDivisor;
        int min = def.anywhereMinCount;

        // Highest-paying fruit, to test the count tiers against.
        SymbolId sym = SymbolId.Cherry; int basePay = 0;
        foreach (var kv in def.payouts)
            if (kv.Value > basePay) { sym = kv.Key; basePay = kv.Value; }

        // Below threshold pays nothing.
        var r = SlotEvaluator.Evaluate(AnywhereGrid(def, sym, min - 1, 0), bet, def);
        Expect($"{min - 1}x {sym} (below {min}) pays nothing", r.totalWin, 0);

        // At threshold pays base * tier(min).
        long wantMin = unit * Mathf.RoundToInt(basePay * def.AnywhereTierMult(min));
        r = SlotEvaluator.Evaluate(AnywhereGrid(def, sym, min, 0), bet, def);
        Expect($"{min}x {sym} pays base*{def.AnywhereTierMult(min)}", r.totalWin, wantMin);

        // A higher tier scales up (10+ where present).
        int high = 10;
        long wantHigh = unit * Mathf.RoundToInt(basePay * def.AnywhereTierMult(high));
        r = SlotEvaluator.Evaluate(AnywhereGrid(def, sym, high, 0), bet, def);
        Expect($"{high}x {sym} pays base*{def.AnywhereTierMult(high)}", r.totalWin, wantHigh);

        // Wild substitutes: (min-1) fruit + 1 wild reaches the threshold.
        r = SlotEvaluator.Evaluate(AnywhereGrid(def, sym, min - 1, 1), bet, def);
        Expect($"{min - 1}x {sym} + 1 wild pays like {min}x", r.totalWin, wantMin);

        // Scatters still award free spins from anywhere.
        var sg = new SymbolId[def.cols, def.rows];
        for (int c = 0; c < def.cols; c++)
            for (int row = 0; row < def.rows; row++)
                sg[c, row] = c < def.scatterCountForFreeSpins && row == 0 ? def.scatterSymbol : SymbolId.Cherry;
        r = SlotEvaluator.Evaluate(sg, bet, def);
        if (!r.isFreeSpinsTriggered || r.freeSpinsAwarded != def.freeSpinsAwarded)
            Fail($"{def.scatterCountForFreeSpins} scatters should award {def.freeSpinsAwarded} free spins");
        else Pass($"scatters award {def.freeSpinsAwarded} free spins + {def.scatterBetMultiplier}x bet");
    }

    static void CheckCatalog(SlotGameDef def)
    {
        var strip = def.reelStrip;

        int runs = 0;
        for (int i = 0; i < strip.Length; i++)
            if (strip[i] == strip[(i + 1) % strip.Length]) runs++;
        if (runs > 0) Fail($"{runs} adjacent duplicate stop(s) — three consecutive stops are read per column, so runs manufacture free wins");
        else Pass($"{strip.Length} stops, no adjacent duplicates");

        foreach (var s in strip)
            if (def.ArtFor(s) == null)
                Fail($"{s} is on the reel strip but has no art — it would render blank");

        foreach (var kv in def.payouts)
            if (Array.IndexOf(strip, kv.Key) < 0)
                Fail($"{kv.Key} pays {kv.Value}x but never lands on the strip");

        if (def.winFavorTable != null)
            foreach (var w in def.winFavorTable)
            {
                if (w.symbol == def.wildSymbol)
                    Fail($"wild {w.symbol} is in the forced-win pool — that hands out the top award and bypasses the multiplier design");
                if (Array.IndexOf(strip, w.symbol) < 0)
                    Fail($"forced-win symbol {w.symbol} is not on the strip");
            }

        // framePath is optional (a machine may render on the velvet backdrop
        // alone); background and card art are required.
        foreach (var path in new[] { def.backgroundPath, def.framePath, def.cardPath })
            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                Fail($"missing art: {path}");

        foreach (var kv in def.symbolArt)
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(kv.Value) == null)
                Fail($"missing symbol art for {kv.Key}: {kv.Value}");
    }

    /// Simulated return per machine. Mirrors SlotMachine.GenerateOutcome so the
    /// number reflects what players actually see, including the forced-win nudge.
    static void CheckBalance()
    {
        const int spins = 200_000;
        const long bet = 10_000;
        var rates = new Dictionary<SlotGameId, double>();

        foreach (var def in SlotCatalog.All)
        {
            var rng = new System.Random(777);
            double wagered = 0, returned = 0;
            long hits = 0;

            int len = def.reelStrip.Length;
            for (int i = 0; i < spins; i++)
            {
                var grid = new SymbolId[def.cols, def.rows];
                for (int c = 0; c < def.cols; c++)
                {
                    int center = rng.Next(0, len);
                    for (int row = 0; row < def.rows; row++)
                        grid[c, row] = def.reelStrip[(center + row - 1 + len) % len];
                }

                long win;
                if (def.payMode == PayMode.AnywhereCount)
                {
                    // Same cascade code the live machine runs, so the RTP here is
                    // the RTP players get.
                    win = SlotCascade.Resolve(grid, bet, def, () => def.reelStrip[rng.Next(0, len)]).totalWin;
                }
                else
                {
                    if (def.winFavorTable != null && def.paylines != null &&
                        rng.NextDouble() < def.guaranteedWinChance)
                    {
                        float total = 0f;
                        foreach (var w in def.winFavorTable) total += w.weight;
                        float roll = (float)rng.NextDouble() * total;
                        var pick = def.winFavorTable[def.winFavorTable.Length - 1].symbol;
                        foreach (var w in def.winFavorTable)
                        {
                            roll -= w.weight;
                            if (roll <= 0f) { pick = w.symbol; break; }
                        }
                        foreach (var coord in def.paylines[rng.Next(0, def.paylines.Length)])
                            grid[coord.col, coord.row] = pick;
                    }
                    win = SlotEvaluator.Evaluate(grid, bet, def).totalWin;
                }

                wagered += bet;
                returned += win;
                if (win > 0) hits++;
            }

            double rtp = returned / wagered * 100.0;
            rates[def.id] = rtp;
            log.AppendLine($"--- {def.displayName}: return {rtp:F0}%, hit {hits * 100.0 / spins:F0}% over {spins:N0} spins ---");
        }

        // No machine may be dramatically more rewarding than another, or the
        // lesser ones become pointless and the coin economy inflates unevenly.
        double lo = double.MaxValue, hi = 0;
        foreach (var kv in rates) { if (kv.Value < lo) lo = kv.Value; if (kv.Value > hi) hi = kv.Value; }
        if (lo > 0 && hi / lo > 1.25)
            Fail($"machines are unbalanced: {lo:F0}% vs {hi:F0}% return (more than 25% apart)");
        else
            Pass($"machines balanced within 25% ({lo:F0}%-{hi:F0}% return)");
    }
}
