using System.Collections.Generic;
using UnityEngine;

public static class SlotEvaluator
{
    public static SpinEvaluationResult Evaluate(SymbolId[,] grid, long totalBet, SlotGameDef def)
    {
        long lineBet = totalBet / def.PaylineCount;
        if (lineBet <= 0) lineBet = 1;

        var result = new SpinEvaluationResult
        {
            winningLines = new List<WinLineResult>(),
            totalWin = 0,
            isFreeSpinsTriggered = false,
            freeSpinsAwarded = 0
        };

        // 1. Paylines
        for (int lineIdx = 0; lineIdx < def.paylines.Length; lineIdx++)
        {
            var coords = def.paylines[lineIdx];
            var s0 = grid[coords[0].col, coords[0].row];
            var s1 = grid[coords[1].col, coords[1].row];
            var s2 = grid[coords[2].col, coords[2].row];

            // Scatters pay from anywhere, so they never form a line win.
            if (s0 == def.scatterSymbol || s1 == def.scatterSymbol || s2 == def.scatterSymbol)
                continue;

            int wilds = CountWilds(def, s0, s1, s2);

            if (CheckMatch(def, s0, s1, s2, out SymbolId winningSym, out int baseMult))
            {
                // Wilds only multiply when they stand in for something else —
                // an all-wild line pays its own flat top award.
                int wildMult = winningSym == def.wildSymbol ? 1 : WildMultiplier(def, wilds);
                int mult = baseMult * wildMult;
                long lineWin = lineBet * mult;

                result.winningLines.Add(new WinLineResult
                {
                    lineIndex = lineIdx,
                    symbol = winningSym,
                    multiplier = mult,
                    winAmount = lineWin,
                    coords = coords,
                    wildMultiplier = wildMult
                });
                result.totalWin += lineWin;
            }
            else if (def.anyComboMultiplier > 0 &&
                     def.IsAnyComboMember(s0) && def.IsAnyComboMember(s1) && def.IsAnyComboMember(s2))
            {
                int wildMult = WildMultiplier(def, wilds);
                int mult = def.anyComboMultiplier * wildMult;
                long lineWin = lineBet * mult;

                result.winningLines.Add(new WinLineResult
                {
                    lineIndex = lineIdx,
                    symbol = def.anyComboSymbol,
                    multiplier = mult,
                    winAmount = lineWin,
                    coords = coords,
                    wildMultiplier = wildMult
                });
                result.totalWin += lineWin;
            }
        }

        // 2. Scatters (free spins bonus)
        int scatterCount = 0;
        for (int c = 0; c < def.cols; c++)
        {
            for (int r = 0; r < def.rows; r++)
            {
                if (grid[c, r] == def.scatterSymbol)
                    scatterCount++;
            }
        }

        if (scatterCount >= def.scatterCountForFreeSpins)
        {
            result.isFreeSpinsTriggered = true;
            result.freeSpinsAwarded = def.freeSpinsAwarded;
            result.totalWin += totalBet * def.scatterBetMultiplier;
        }

        // 3. Celebration tier
        result.totalMultiplier = (float)result.totalWin / Mathf.Max(1, totalBet);
        if (result.totalMultiplier >= 50f)
            result.tier = WinCelebrationTier.EpicWin;
        else if (result.totalMultiplier >= 25f)
            result.tier = WinCelebrationTier.MegaWin;
        else if (result.totalMultiplier >= 10f)
            result.tier = WinCelebrationTier.BigWin;
        else if (result.totalWin > 0)
            result.tier = WinCelebrationTier.Normal;
        else
            result.tier = WinCelebrationTier.None;

        return result;
    }

    static int CountWilds(SlotGameDef def, SymbolId a, SymbolId b, SymbolId c)
    {
        int n = 0;
        if (a == def.wildSymbol) n++;
        if (b == def.wildSymbol) n++;
        if (c == def.wildSymbol) n++;
        return n;
    }

    /// wildLineMultiplier ^ wildCount — Triple Diamond's 3x per diamond, so two
    /// diamonds on a line pay 9x. A multiplier of 1 makes this a no-op.
    static int WildMultiplier(SlotGameDef def, int wildCount)
    {
        if (def.wildLineMultiplier <= 1 || wildCount <= 0) return 1;
        int m = 1;
        for (int i = 0; i < wildCount; i++) m *= def.wildLineMultiplier;
        return m;
    }

    static bool CheckMatch(SlotGameDef def, SymbolId a, SymbolId b, SymbolId c,
                           out SymbolId winSym, out int mult)
    {
        winSym = a;
        mult = 0;

        var wild = def.wildSymbol;

        // Candidate is the first non-wild, or the wild itself when all three are.
        SymbolId target = wild;
        if (a != wild) target = a;
        else if (b != wild) target = b;
        else if (c != wild) target = c;

        bool matchA = (a == target || a == wild);
        bool matchB = (b == target || b == wild);
        bool matchC = (c == target || c == wild);

        if (matchA && matchB && matchC)
        {
            winSym = target;
            mult = def.PayoutFor(target);
            return mult > 0;
        }

        return false;
    }
}
