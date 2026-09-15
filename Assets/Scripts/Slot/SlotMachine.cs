using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SlotState
{
    Idle,
    Spinning,
    Evaluating,
    Celebrating
}

public class SlotMachine : MonoBehaviour
{
    /// Which machine this scene is. Baked in by the scene builder; the lobby
    /// picks the scene, not this.
    public SlotGameId gameId = SlotGameId.Classic777;

    public ReelColumn[] reels;
    public Sprite[] symbolSprites;
    public SlotUI ui;
    public SlotWinPopup winPopup;

    public SlotGameDef Def { get; private set; }

    public SlotState State { get; private set; } = SlotState.Idle;

    public int BetIndex { get; private set; } = 3; // default 10,000
    public long CurrentBet => Def.betLadder[Mathf.Clamp(BetIndex, 0, Def.betLadder.Length - 1)];

    public bool IsAutoSpin { get; private set; } = false;
    public int FreeSpinsRemaining { get; private set; } = 0;
    public bool IsFreeSpinsActive => FreeSpinsRemaining > 0;

    int reelsStoppedCount = 0;
    SymbolId[,] finalOutcome;
    SpinEvaluationResult lastEvaluation;

    void Awake()
    {
        Def = SlotCatalog.Get(gameId);
        finalOutcome = new SymbolId[Def.cols, Def.rows];
        BetIndex = Mathf.Clamp(BetIndex, 0, Def.betLadder.Length - 1);
    }

    [System.Serializable]
    public struct SymbolEntry
    {
        public SymbolId id;
        public Sprite sprite;
    }

    public SymbolEntry[] symbolEntries;

    public Sprite GetSprite(SymbolId id)
    {
        if (symbolEntries != null)
        {
            for (int i = 0; i < symbolEntries.Length; i++)
            {
                if (symbolEntries[i].id == id && symbolEntries[i].sprite != null)
                    return symbolEntries[i].sprite;
            }
        }
        return null;
    }

    void Start()
    {
        for (int c = 0; c < reels.Length; c++)
        {
            reels[c].Init(c, Def.rows, Def.reelStrip, Def.rowHeight, GetSprite, OnReelFinished);
        }
        ui.RefreshBet(CurrentBet);
        ui.RefreshBalances();
    }

    public void ChangeBet(int delta)
    {
        if (State != SlotState.Idle || IsFreeSpinsActive) return;
        BetIndex = Mathf.Clamp(BetIndex + delta, 0, Def.betLadder.Length - 1);
        ui.RefreshBet(CurrentBet);
        AudioManager.PlayClick();
    }

    public void SetMaxBet()
    {
        if (State != SlotState.Idle || IsFreeSpinsActive) return;
        BetIndex = Def.betLadder.Length - 1;
        ui.RefreshBet(CurrentBet);
        AudioManager.PlayClick();
    }

    public void ToggleAutoSpin()
    {
        IsAutoSpin = !IsAutoSpin;
        ui.SetAutoSpinState(IsAutoSpin);
        AudioManager.PlayClick();

        if (IsAutoSpin && State == SlotState.Idle)
        {
            TrySpin();
        }
    }

    public void StopAutoSpin()
    {
        IsAutoSpin = false;
        ui.SetAutoSpinState(false);
    }

    public void TrySpin()
    {
        if (State != SlotState.Idle) return;

        var state = GameState.I;
        long bet = CurrentBet;

        // In free spins, spin is free!
        if (IsFreeSpinsActive)
        {
            FreeSpinsRemaining--;
            ui.UpdateFreeSpins(FreeSpinsRemaining);
        }
        else
        {
            // Deduct bet
            if (state != null && !state.TrySpendCoins(bet))
            {
                StopAutoSpin();
                ui.ShowInsufficientFunds();
                return;
            }
        }

        ui.RefreshBalances();
        ui.ClearWinDisplay();
        ResetSymbolHighlights();

        StartCoroutine(SpinSequenceRoutine(bet));
    }

    IEnumerator SpinSequenceRoutine(long bet)
    {
        State = SlotState.Spinning;
        reelsStoppedCount = 0;
        ui.SetSpinButtonInteractable(false);

        // Determine final outcome
        finalOutcome = GenerateOutcome();
        lastEvaluation = SlotEvaluator.Evaluate(finalOutcome, bet, Def);

        // Free spins pay a multiple of the normal win.
        if (IsFreeSpinsActive && Def.freeSpinWinMultiplier > 1)
        {
            lastEvaluation.totalWin *= Def.freeSpinWinMultiplier;
        }

        // Start spinning all reels
        for (int c = 0; c < reels.Length; c++)
        {
            reels[c].StartSpin();
        }

        // Spin duration before stopping reels in cascade
        yield return new WaitForSeconds(0.6f);

        // Cascade stop with delays
        for (int c = 0; c < reels.Length; c++)
        {
            var colSymbols = new SymbolId[Def.rows];
            for (int r = 0; r < Def.rows; r++)
                colSymbols[r] = finalOutcome[c, r];

            reels[c].RequestStop(colSymbols);
            yield return new WaitForSeconds(0.25f);
        }

        // Wait until all reels have completed their bounce-back
        while (reelsStoppedCount < reels.Length)
            yield return null;

        // Evaluate and present wins
        yield return StartCoroutine(PresentWinsRoutine(bet));
    }

    void OnReelFinished(int colIdx)
    {
        reelsStoppedCount++;
        AudioManager.PlayClick();
    }

    IEnumerator PresentWinsRoutine(long bet)
    {
        State = SlotState.Evaluating;

        // Record spin to GameState and quests
        var state = GameState.I;
        long prevCoins = state != null ? state.Data.coins : 0;
        bool won = false;
        if (state != null)
        {
            state.RecordSpin(bet, lastEvaluation.totalWin);
            if (lastEvaluation.totalWin > 0)
            {
                state.AddCoins(lastEvaluation.totalWin, RewardSource.Win);
                won = true;
            }
        }

        // A win animates the coin balance counting up with flying coins;
        // anything else (no win, or no GameState) just snaps both balances.
        if (won)
        {
            ui.RefreshGemsOnly();
            ui.AnimateCoinGain(prevCoins, state.Data.coins);
        }
        else
        {
            ui.RefreshBalances();
        }

        // Check Free Spins trigger
        if (lastEvaluation.isFreeSpinsTriggered)
        {
            FreeSpinsRemaining += lastEvaluation.freeSpinsAwarded;
            ui.ShowFreeSpinsBanner(FreeSpinsRemaining);
            yield return new WaitForSeconds(1.5f);
        }

        if (lastEvaluation.totalWin > 0)
        {
            ui.ShowWinAmount(lastEvaluation.totalWin);

            // Highlight winning symbols
            HighlightWinningPaylines();

            // Big win celebration popup if high multiplier
            if (lastEvaluation.tier >= WinCelebrationTier.BigWin && winPopup != null)
            {
                State = SlotState.Celebrating;
                bool popupDone = false;
                winPopup.Show(lastEvaluation.tier, lastEvaluation.totalWin, () => popupDone = true);
                while (!popupDone) yield return null;
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
        }

        State = SlotState.Idle;
        ui.SetSpinButtonInteractable(true);

        // Continue Auto Spin or Free Spins
        if (IsFreeSpinsActive)
        {
            yield return new WaitForSeconds(0.5f);
            TrySpin();
        }
        else if (IsAutoSpin)
        {
            yield return new WaitForSeconds(0.6f);
            if (IsAutoSpin) TrySpin();
        }
    }

    static readonly Color[] PaylineColors = new[]
    {
        new Color(1f, 0.85f, 0.2f, 1f), // Line 0 (mid): Gold
        new Color(0.3f, 0.85f, 1f, 1f), // Line 1 (top): Cyan
        new Color(1f, 0.4f, 0.8f, 1f),  // Line 2 (bot): Magenta
        new Color(0.4f, 1f, 0.4f, 1f),  // Line 3 (diag down): Green
        new Color(1f, 0.6f, 0.2f, 1f)   // Line 4 (diag up): Orange
    };

    void HighlightWinningPaylines()
    {
        // Dim all symbols first
        for (int c = 0; c < Def.cols && c < reels.Length; c++)
        {
            for (int r = 0; r < Def.rows; r++)
                reels[c].HighlightSymbol(r, false);
        }

        // Highlight winning symbols with payline-specific glowing borders
        for (int i = 0; i < lastEvaluation.winningLines.Count; i++)
        {
            var win = lastEvaluation.winningLines[i];
            Color col = PaylineColors[win.lineIndex % PaylineColors.Length];
            foreach (var coord in win.coords)
            {
                reels[coord.col].HighlightSymbol(coord.row, true, col);
            }
        }
    }

    void ResetSymbolHighlights()
    {
        for (int c = 0; c < Def.cols && c < reels.Length; c++)
        {
            reels[c].ResetHighlights();
        }
    }

    SymbolId[,] GenerateOutcome()
    {
        var grid = new SymbolId[Def.cols, Def.rows];
        var strip = Def.reelStrip;
        int stripLen = strip.Length;

        // Base random selection
        for (int c = 0; c < Def.cols; c++)
        {
            int centerIdx = UnityEngine.Random.Range(0, stripLen);
            for (int r = 0; r < Def.rows; r++)
            {
                int idx = (centerIdx + r - 1 + stripLen) % stripLen;
                grid[c, r] = strip[idx];
            }
        }

        // Nudge a share of spins into a guaranteed line win so the game stays
        // lively. The pool excludes top-award symbols on purpose.
        if (UnityEngine.Random.value < Def.guaranteedWinChance)
        {
            int winLine = UnityEngine.Random.Range(0, Def.paylines.Length);
            var winSym = PickFavoredSymbol();

            foreach (var coord in Def.paylines[winLine])
            {
                grid[coord.col, coord.row] = winSym;
            }
        }

        return grid;
    }

    SymbolId PickFavoredSymbol()
    {
        var table = Def.winFavorTable;
        if (table == null || table.Length == 0) return SymbolId.Ten;

        float total = 0f;
        for (int i = 0; i < table.Length; i++) total += table[i].weight;
        if (total <= 0f) return table[0].symbol;

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < table.Length; i++)
        {
            roll -= table[i].weight;
            if (roll <= 0f) return table[i].symbol;
        }
        return table[table.Length - 1].symbol;
    }
}
