using System.Collections.Generic;
using UnityEngine;

public static class SlotEvaluator
{
    public static SpinEvaluationResult Evaluate(SymbolId[,] grid, long totalBet)
    {
        long lineBet = totalBet / SlotDef.PaylineCount;
        if (lineBet <= 0) lineBet = 1;

        var result = new SpinEvaluationResult
        {
            winningLines = new List<WinLineResult>(),
            totalWin = 0,
            isFreeSpinsTriggered = false,
            freeSpinsAwarded = 0
        };

        // 1. Evaluate 5 Paylines
        for (int lineIdx = 0; lineIdx < SlotDef.Paylines.Length; lineIdx++)
        {
            var coords = SlotDef.Paylines[lineIdx];
            var s0 = grid[coords[0].col, coords[0].row];
            var s1 = grid[coords[1].col, coords[1].row];
            var s2 = grid[coords[2].col, coords[2].row];

            // Ignore scatter on paylines (scatters pay anywhere)
            if (s0 == SymbolId.Scatter || s1 == SymbolId.Scatter || s2 == SymbolId.Scatter)
                continue;

            // Check match with Wild substitution
            if (CheckMatch(s0, s1, s2, out SymbolId winningSym, out int mult))
            {
                long lineWin = lineBet * mult;
                result.winningLines.Add(new WinLineResult
                {
                    lineIndex = lineIdx,
                    symbol = winningSym,
                    multiplier = mult,
                    winAmount = lineWin,
                    coords = coords
                });
                result.totalWin += lineWin;
            }
            // Check Any 7s combo (combination of Red 7 and Gold 7)
            else if (IsAnySeven(s0) && IsAnySeven(s1) && IsAnySeven(s2))
            {
                int multAny7 = 50;
                long lineWin = lineBet * multAny7;
                result.winningLines.Add(new WinLineResult
                {
                    lineIndex = lineIdx,
                    symbol = SymbolId.AnySeven,
                    multiplier = multAny7,
                    winAmount = lineWin,
                    coords = coords
                });
                result.totalWin += lineWin;
            }
        }

        // 2. Evaluate Scatters (Free Spins bonus)
        int scatterCount = 0;
        for (int c = 0; c < SlotDef.Cols; c++)
        {
            for (int r = 0; r < SlotDef.Rows; r++)
            {
                if (grid[c, r] == SymbolId.Scatter)
                    scatterCount++;
            }
        }

        if (scatterCount >= 3)
        {
            result.isFreeSpinsTriggered = true;
            result.freeSpinsAwarded = 10;
            // Also pays 5x total bet
            result.totalWin += totalBet * 5;
        }

        // Determine celebration tier
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

    static bool CheckMatch(SymbolId a, SymbolId b, SymbolId c, out SymbolId winSym, out int mult)
    {
        winSym = SymbolId.Ten;
        mult = 0;

        // Determine candidate symbol (first non-wild, or Wild if all 3 are wild)
        SymbolId target = SymbolId.Wild;
        if (a != SymbolId.Wild) target = a;
        else if (b != SymbolId.Wild) target = b;
        else if (c != SymbolId.Wild) target = c;

        bool matchA = (a == target || a == SymbolId.Wild);
        bool matchB = (b == target || b == SymbolId.Wild);
        bool matchC = (c == target || c == SymbolId.Wild);

        if (matchA && matchB && matchC)
        {
            winSym = target;
            mult = SlotDef.Get3OfAKindMultiplier(target);
            return true;
        }

        return false;
    }

    static bool IsAnySeven(SymbolId s)
    {
        return s == SymbolId.SevenRed || s == SymbolId.SevenGold || s == SymbolId.Wild;
    }
}
