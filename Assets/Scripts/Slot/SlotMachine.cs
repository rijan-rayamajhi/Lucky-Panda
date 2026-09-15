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
    public ReelColumn[] reels; // 3 reels
    public Sprite[] symbolSprites;
    public SlotUI ui;
    public SlotWinPopup winPopup;

    public SlotState State { get; private set; } = SlotState.Idle;

    public int BetIndex { get; private set; } = 3; // default $10,000
    public long CurrentBet => SlotDef.BetLadder[BetIndex];

    public bool IsAutoSpin { get; private set; } = false;
    public int FreeSpinsRemaining { get; private set; } = 0;
    public bool IsFreeSpinsActive => FreeSpinsRemaining > 0;

    int reelsStoppedCount = 0;
    SymbolId[,] finalOutcome = new SymbolId[SlotDef.Cols, SlotDef.Rows];
    SpinEvaluationResult lastEvaluation;

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
            reels[c].Init(c, 150f, GetSprite, OnReelFinished);
        }
        ui.RefreshBet(CurrentBet);
        ui.RefreshBalances();
    }

    public void ChangeBet(int delta)
    {
        if (State != SlotState.Idle || IsFreeSpinsActive) return;
        BetIndex = Mathf.Clamp(BetIndex + delta, 0, SlotDef.BetLadder.Length - 1);
        ui.RefreshBet(CurrentBet);
        AudioManager.PlayClick();
    }

    public void SetMaxBet()
    {
        if (State != SlotState.Idle || IsFreeSpinsActive) return;
        BetIndex = SlotDef.BetLadder.Length - 1;
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
        lastEvaluation = SlotEvaluator.Evaluate(finalOutcome, bet);

        // Apply 2x multiplier during Free Spins!
        if (IsFreeSpinsActive)
        {
            lastEvaluation.totalWin *= 2;
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
            var colSymbols = new SymbolId[SlotDef.Rows];
            for (int r = 0; r < SlotDef.Rows; r++)
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
        if (state != null)
        {
            state.RecordSpin(bet, lastEvaluation.totalWin);
            if (lastEvaluation.totalWin > 0)
            {
                state.AddCoins(lastEvaluation.totalWin, RewardSource.Win);
            }
        }

        ui.RefreshBalances();

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
        for (int c = 0; c < SlotDef.Cols; c++)
        {
            for (int r = 0; r < SlotDef.Rows; r++)
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
        for (int c = 0; c < SlotDef.Cols; c++)
        {
            reels[c].ResetHighlights();
        }
    }

    SymbolId[,] GenerateOutcome()
    {
        var grid = new SymbolId[SlotDef.Cols, SlotDef.Rows];
        int stripLen = SlotDef.ReelStrip.Length;

        // Base random selection
        for (int c = 0; c < SlotDef.Cols; c++)
        {
            int centerIdx = UnityEngine.Random.Range(0, stripLen);
            for (int r = 0; r < SlotDef.Rows; r++)
            {
                int idx = (centerIdx + r - 1 + stripLen) % stripLen;
                grid[c, r] = SlotDef.ReelStrip[idx];
            }
        }

        // 35% chance of guaranteed win to ensure lively exciting gameplay
        if (UnityEngine.Random.value < 0.35f)
        {
            int winLine = UnityEngine.Random.Range(0, SlotDef.Paylines.Length);
            SymbolId winSym;
            float roll = UnityEngine.Random.value;
            if (roll < 0.05f) winSym = SymbolId.SevenRed;       // 5% Red 7 jackpot
            else if (roll < 0.12f) winSym = SymbolId.SevenGold; // 7% Gold 7
            else if (roll < 0.25f) winSym = SymbolId.Bar;       // 13% Bar
            else if (roll < 0.45f) winSym = SymbolId.Ace;       // 20% Ace
            else if (roll < 0.70f) winSym = SymbolId.King;      // 25% King
            else winSym = SymbolId.Queen;

            foreach (var coord in SlotDef.Paylines[winLine])
            {
                grid[coord.col, coord.row] = winSym;
            }
        }

        return grid;
    }
}
