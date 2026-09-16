using System;
using System.Collections.Generic;
using UnityEngine;

/// One coin that landed during the Hold & Win bonus, with the cell it locked in.
public struct HoldCoinPlacement
{
    public int col;
    public int row;
    public CoinFace face;
}

/// One respin of the bonus: the coins that landed this respin and the respins
/// left afterward (reset to full on any land, otherwise decremented).
public struct HoldStep
{
    public List<HoldCoinPlacement> newCoins;
    public int respinsLeft;
    public int coinCount;   // coins locked on the board after this respin
}

public struct HoldResult
{
    public long totalWin;
    public List<HoldStep> steps;
    public CoinFace?[,] finalGrid;
    public int coinCount;
    public bool grandAwarded;
    public long grandValue;
}

/// The Hold & Win (coin respin / link) bonus, shared by the live machine
/// (SlotMachine) and the headless balance sim (SlotMathCheck), so the RTP the
/// sim verifies is the RTP players get. Lock the triggering coins, respin only
/// empty cells; any new coin re-locks and resets respins to full; the bonus
/// ends at zero respins or a full board (which pays the Grand).
///
/// `rand01` is the single RNG seam: UnityEngine.Random at runtime,
/// System.Random in the sim.
public static class HoldAndWin
{
    /// Draw one coin face — a fixed jackpot (Mini/Minor/Major) or a weighted
    /// cash value (`credits` * betUnit). Grand never lands on a coin; it is the
    /// board-full award.
    public static CoinFace DrawCoinFace(SlotGameDef def, long totalBet, Func<double> rand01)
    {
        long betUnit = totalBet / Mathf.Max(1, def.anywhereBetDivisor);
        if (betUnit <= 0) betUnit = 1;

        if (rand01() < def.jackpotCoinChance)
        {
            double r = rand01();
            JackpotTier t = r < def.jackpotMiniShare ? JackpotTier.Mini
                : r < def.jackpotMiniShare + def.jackpotMinorShare ? JackpotTier.Minor
                : JackpotTier.Major;
            return new CoinFace(def.JackpotBetMultiplier(t) * totalBet, t);
        }

        var table = def.coinValueTable;
        if (table == null || table.Length == 0) return new CoinFace(betUnit, JackpotTier.None);

        float total = 0f;
        for (int i = 0; i < table.Length; i++) total += table[i].weight;
        double roll = rand01() * total;
        int credits = table[table.Length - 1].credits;
        for (int i = 0; i < table.Length; i++)
        {
            roll -= table[i].weight;
            if (roll <= 0) { credits = table[i].credits; break; }
        }
        return new CoinFace(credits * betUnit, JackpotTier.None);
    }

    /// `initial` holds the triggering coins' faces (null = empty cell). Resolves
    /// the whole bonus up front; the machine plays the steps back.
    public static HoldResult Resolve(CoinFace?[,] initial, long totalBet, SlotGameDef def, Func<double> rand01)
    {
        int cols = def.cols, rows = def.rows, cells = cols * rows;
        var grid = (CoinFace?[,])initial.Clone();

        int filled = 0;
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                if (grid[c, r] != null) filled++;

        var res = new HoldResult { steps = new List<HoldStep>() };
        int respins = def.holdRespins;

        while (respins > 0 && filled < cells)
        {
            var landed = new List<HoldCoinPlacement>();
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (grid[c, r] != null) continue;
                    if (rand01() < def.coinRespinLandChance)
                    {
                        var face = DrawCoinFace(def, totalBet, rand01);
                        grid[c, r] = face;
                        filled++;
                        landed.Add(new HoldCoinPlacement { col = c, row = r, face = face });
                    }
                }
            }

            respins = landed.Count > 0 ? def.holdRespins : respins - 1;
            res.steps.Add(new HoldStep { newCoins = landed, respinsLeft = respins, coinCount = filled });
        }

        long win = 0;
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                if (grid[c, r] != null) win += grid[c, r].Value.value;

        if (filled >= cells)
        {
            res.grandValue = (long)def.grandBetMultiplier * totalBet;
            res.grandAwarded = true;
            win += res.grandValue;
        }

        res.finalGrid = grid;
        res.coinCount = filled;
        res.totalWin = win;
        return res;
    }
}
