using System;
using System.Collections.Generic;
using UnityEngine;

/// One resolved cascade step: the wins on the grid at the start of the step,
/// the chain multiplier applied, which cells burst, and the grid after the
/// survivors drop and new symbols refill from the top.
public struct CascadeStep
{
    public SpinEvaluationResult eval;
    public int chainMultiplier;
    public long stepWin;
    public bool[,] cleared;
    public SymbolId[,] resultGrid;
}

public struct CascadeResult
{
    public long totalWin;
    public List<CascadeStep> steps;
    public bool freeSpinsTriggered;
    public int freeSpinsAwarded;
    public int chainLength;
    public WinCelebrationTier tier;
}

/// The cascade loop, shared by the live machine (SlotMachine) and the headless
/// balance sim (SlotMathCheck), so the RTP the sim verifies is the RTP players
/// get. Evaluate → burst winning cells → drop survivors → refill from the top →
/// re-evaluate with the next chain multiplier, until no win or maxCascades.
///
/// `refill` supplies the symbol dropped into an emptied top cell — the single
/// RNG seam (UnityEngine.Random at runtime, System.Random in the sim).
public static class SlotCascade
{
    public static CascadeResult Resolve(SymbolId[,] initial, long totalBet, SlotGameDef def, Func<SymbolId> refill)
    {
        int cols = def.cols, rows = def.rows;
        var grid = (SymbolId[,])initial.Clone();
        var res = new CascadeResult { steps = new List<CascadeStep>() };

        int step = 0;
        while (true)
        {
            bool first = step == 0;
            var eval = SlotEvaluator.Evaluate(grid, totalBet, def, includeScatter: first);

            if (first && eval.isFreeSpinsTriggered)
            {
                res.freeSpinsTriggered = true;
                res.freeSpinsAwarded = eval.freeSpinsAwarded;
            }

            if (eval.winningLines.Count == 0)
                break;

            int mult = def.CascadeMult(step);
            long stepWin = eval.totalWin * mult;
            res.totalWin += stepWin;

            var cleared = new bool[cols, rows];
            foreach (var w in eval.winningLines)
                foreach (var cd in w.coords)
                    cleared[cd.col, cd.row] = true;

            var next = Collapse(grid, cleared, def, refill);
            res.steps.Add(new CascadeStep
            {
                eval = eval,
                chainMultiplier = mult,
                stepWin = stepWin,
                cleared = cleared,
                resultGrid = next
            });

            grid = next;
            step++;
            // ponytail: hard depth cap — guards a chain that never settles
            if (step >= def.maxCascades) break;
        }

        res.chainLength = step;
        float m = totalBet > 0 ? (float)res.totalWin / totalBet : 0f;
        res.tier = res.totalWin <= 0 ? WinCelebrationTier.None
            : m >= 50f ? WinCelebrationTier.EpicWin
            : m >= 25f ? WinCelebrationTier.MegaWin
            : m >= 10f ? WinCelebrationTier.BigWin
            : WinCelebrationTier.Normal;
        return res;
    }

    /// Survivors keep their top-to-bottom order and settle at the bottom (higher
    /// row index); emptied cells at the top refill. Scatters are never in a win
    /// so they simply fall like any survivor.
    static SymbolId[,] Collapse(SymbolId[,] grid, bool[,] cleared, SlotGameDef def, Func<SymbolId> refill)
    {
        int cols = def.cols, rows = def.rows;
        var outg = new SymbolId[cols, rows];
        var survivors = new List<SymbolId>(rows);

        for (int c = 0; c < cols; c++)
        {
            survivors.Clear();
            for (int r = 0; r < rows; r++)
                if (!cleared[c, r]) survivors.Add(grid[c, r]);

            int need = rows - survivors.Count;
            for (int r = 0; r < need; r++) outg[c, r] = refill();
            for (int i = 0; i < survivors.Count; i++) outg[c, need + i] = survivors[i];
        }
        return outg;
    }
}
