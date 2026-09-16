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
    /// 0 while auto-spinning = infinite; >0 = spins left before it stops.
    public int AutoSpinsRemaining { get; private set; } = 0;
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

    /// Start auto-spin for `count` spins (0 = infinite), from the count picker.
    public void StartAutoSpins(int count)
    {
        IsAutoSpin = true;
        AutoSpinsRemaining = count;
        ui.SetAutoSpinState(true);
        if (State == SlotState.Idle) TrySpin();
    }

    public void StopAutoSpin()
    {
        IsAutoSpin = false;
        AutoSpinsRemaining = 0;
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

        // Determine final outcome. Cascade games resolve after the reels stop
        // (the chain runs on the shown grid), so only payline games pre-evaluate.
        finalOutcome = GenerateOutcome();
        // Paylines and Hold & Win both score the shown grid in one evaluation;
        // only AnywhereCount runs the post-stop cascade chain.
        if (Def.payMode != PayMode.AnywhereCount)
        {
            lastEvaluation = SlotEvaluator.Evaluate(finalOutcome, bet, Def);

            // Free spins pay a multiple of the normal win.
            if (IsFreeSpinsActive && Def.freeSpinWinMultiplier > 1)
                lastEvaluation.totalWin *= Def.freeSpinWinMultiplier;
        }

        // Start spinning all reels
        AudioManager.PlaySpinSound();
        AudioManager.Haptic();
        for (int c = 0; c < reels.Length; c++)
        {
            reels[c].StartSpin();
        }

        // Spin duration before stopping reels in cascade
        yield return new WaitForSeconds(0.6f);

        // Cascade stop with delays. Anticipation: once enough feature symbols
        // (bonus coins for Hold & Win, scatters otherwise) have landed that the
        // next reels could trigger the feature, the remaining reels stop slower
        // for tension.
        SymbolId featureSym = Def.payMode == PayMode.HoldAndWin ? Def.coinSymbol : Def.scatterSymbol;
        int featureNeed = Def.payMode == PayMode.HoldAndWin ? Def.coinsToTriggerHold : Def.scatterCountForFreeSpins;
        int featureSeen = 0;
        for (int c = 0; c < reels.Length; c++)
        {
            var colSymbols = new SymbolId[Def.rows];
            for (int r = 0; r < Def.rows; r++)
            {
                colSymbols[r] = finalOutcome[c, r];
                if (colSymbols[r] == featureSym) featureSeen++;
            }

            reels[c].RequestStop(colSymbols);

            // Reels still to stop could complete the feature, and we're within a
            // couple of the trigger: draw out the suspense.
            int reelsLeft = reels.Length - 1 - c;
            bool anticipating = reelsLeft > 0 && featureNeed - featureSeen <= reelsLeft
                                && featureSeen >= featureNeed - 2;   // within two of the trigger
            yield return new WaitForSeconds(anticipating ? 0.7f : 0.25f);
        }

        // Wait until all reels have completed their bounce-back
        while (reelsStoppedCount < reels.Length)
            yield return null;

        AudioManager.StopSpinSound();

        // Evaluate and present wins
        if (Def.payMode == PayMode.AnywhereCount)
            yield return StartCoroutine(PresentCascadeRoutine(bet));
        else if (Def.payMode == PayMode.HoldAndWin)
            yield return StartCoroutine(PresentHoldAndWinRoutine(bet));
        else
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
        if (lastEvaluation.totalWin > 0)
            HighlightWinningPaylines();
        yield return StartCoroutine(FinishSpin(bet, lastEvaluation.totalWin, lastEvaluation.tier,
            lastEvaluation.isFreeSpinsTriggered, lastEvaluation.freeSpinsAwarded));
    }

    // Pay-anywhere cascade: resolve the whole chain up front (deterministic —
    // the refills we show are exactly the ones scored), then play it back step
    // by step, bursting winners, dropping the resolved refills and raising the
    // chain multiplier, before crediting the accumulated win.
    IEnumerator PresentCascadeRoutine(long bet)
    {
        State = SlotState.Evaluating;

        var strip = Def.reelStrip;
        int len = strip.Length;
        var cascade = SlotCascade.Resolve(finalOutcome, bet, Def,
            () => strip[UnityEngine.Random.Range(0, len)]);

        long totalWin = cascade.totalWin;
        if (IsFreeSpinsActive && Def.freeSpinWinMultiplier > 1)
            totalWin *= Def.freeSpinWinMultiplier;

        long running = 0;
        for (int i = 0; i < cascade.steps.Count; i++)
        {
            var s = cascade.steps[i];
            ui.ShowChainMultiplier(s.chainMultiplier);
            HighlightAnywhereWins(s.eval);
            running += s.stepWin;
            ui.ShowWinAmount(running);
            AudioManager.PlayClick();
            yield return new WaitForSeconds(0.55f);

            yield return StartCoroutine(CollapseColumns(s.cleared, s.resultGrid));
            ResetSymbolHighlights();
        }
        ui.HideChainMultiplier();

        yield return StartCoroutine(FinishSpin(bet, totalWin, cascade.tier,
            cascade.freeSpinsTriggered, cascade.freeSpinsAwarded));
    }

    // Credit the win, roll balances, fire free-spins/celebration, then release
    // the machine and continue auto/free spins. Shared by both pay modes.
    IEnumerator FinishSpin(long bet, long totalWin, WinCelebrationTier tier,
                           bool freeSpinsTriggered, int freeSpinsAwarded)
    {
        var state = GameState.I;
        long prevCoins = state != null ? state.Data.coins : 0;
        long credited = totalWin;   // what the player actually receives (club multiplier applied)
        bool won = false;
        if (state != null)
        {
            state.RecordSpin(bet, totalWin);
            if (totalWin > 0)
            {
                credited = state.AddCoins(totalWin, RewardSource.Win);
                won = true;
            }
        }

        if (won)
        {
            ui.RefreshGemsOnly();
            ui.AnimateCoinGain(prevCoins, state.Data.coins);
        }
        else
        {
            ui.RefreshBalances();
        }

        if (freeSpinsTriggered)
        {
            FreeSpinsRemaining += freeSpinsAwarded;
            ui.ShowFreeSpinsBanner(FreeSpinsRemaining);
            yield return new WaitForSeconds(1.5f);
        }

        if (totalWin > 0)
        {
            ui.ShowWinAmount(credited);

            if (tier >= WinCelebrationTier.BigWin && winPopup != null)
            {
                // Bigger tiers get more dings + a buzz.
                AudioManager.PlayWinCelebration((int)tier - (int)WinCelebrationTier.BigWin + 1);
                AudioManager.Haptic();
                State = SlotState.Celebrating;
                bool popupDone = false;
                winPopup.Show(tier, credited, () => popupDone = true);
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

        if (IsFreeSpinsActive)
        {
            yield return new WaitForSeconds(0.5f);
            TrySpin();
        }
        else if (IsAutoSpin)
        {
            // Count down a finite auto-spin batch and stop when it runs out.
            if (AutoSpinsRemaining > 0)
            {
                AutoSpinsRemaining--;
                ui.SetAutoSpinState(true);
                if (AutoSpinsRemaining == 0) { StopAutoSpin(); yield break; }
            }

            // A Mega/Epic win is worth stopping for rather than spinning past.
            if (tier >= WinCelebrationTier.MegaWin)
            {
                StopAutoSpin();
            }
            else
            {
                yield return new WaitForSeconds(0.6f);
                if (IsAutoSpin) TrySpin();
            }
        }
    }

    // Burst every column's cleared cells and settle to the resolved grid in
    // parallel, then wait for the slowest column.
    IEnumerator CollapseColumns(bool[,] cleared, SymbolId[,] resultGrid)
    {
        int running = 0;
        for (int c = 0; c < Def.cols && c < reels.Length; c++)
        {
            var clearedCol = new bool[Def.rows];
            var finalCol = new SymbolId[Def.rows];
            for (int r = 0; r < Def.rows; r++)
            {
                clearedCol[r] = cleared[c, r];
                finalCol[r] = resultGrid[c, r];
            }
            running++;
            StartCoroutine(RunColumnCollapse(reels[c], clearedCol, finalCol, () => running--));
        }
        while (running > 0) yield return null;
    }

    IEnumerator RunColumnCollapse(ReelColumn reel, bool[] cleared, SymbolId[] finalCol, Action done)
    {
        yield return reel.BurstAndRefill(cleared, finalCol);
        done();
    }

    static readonly Color CoinGold = new Color(1f, 0.84f, 0.3f, 1f);

    static string CoinLabel(CoinFace f) =>
        f.jackpot != JackpotTier.None
            ? f.jackpot.ToString().ToUpper()
            : f.value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    // Hold & Win: present the base anywhere win, then — if enough collector
    // coins landed — lock them and play the coin respin bonus. The bonus is
    // resolved up front (HoldAndWin.Resolve, the same code the balance sim runs)
    // and played back step by step before the total is credited.
    IEnumerator PresentHoldAndWinRoutine(long bet)
    {
        State = SlotState.Evaluating;

        long baseWin = lastEvaluation.totalWin;
        if (baseWin > 0) HighlightAnywhereWins(lastEvaluation);

        int coinCount = 0;
        for (int c = 0; c < Def.cols; c++)
            for (int r = 0; r < Def.rows; r++)
                if (finalOutcome[c, r] == Def.coinSymbol) coinCount++;

        // Not enough coins — settle the base win like a normal spin.
        if (coinCount < Def.coinsToTriggerHold)
        {
            yield return StartCoroutine(FinishSpin(bet, baseWin, lastEvaluation.tier, false, 0));
            yield break;
        }

        if (baseWin > 0) { ui.ShowWinAmount(baseWin); yield return new WaitForSeconds(0.6f); }
        ResetSymbolHighlights();

        System.Func<double> rand01 = () => (double)UnityEngine.Random.value;

        // Seed the board with the triggering coins' faces and resolve the bonus.
        var board = new CoinFace?[Def.cols, Def.rows];
        long running = baseWin;
        for (int c = 0; c < Def.cols; c++)
            for (int r = 0; r < Def.rows; r++)
                if (finalOutcome[c, r] == Def.coinSymbol)
                {
                    var face = HoldAndWin.DrawCoinFace(Def, bet, rand01);
                    board[c, r] = face;
                    running += face.value;
                }

        var seeded = (CoinFace?[,])board.Clone();
        var hold = HoldAndWin.Resolve(seeded, bet, Def, rand01);

        // Lock: coins glow gold, every other cell dims.
        ui.ShowHoldBanner(Def.holdRespins);
        for (int c = 0; c < Def.cols; c++)
            for (int r = 0; r < Def.rows; r++)
            {
                if (board[c, r] != null)
                {
                    reels[c].SetSymbol(r, Def.coinSymbol);
                    reels[c].HighlightSymbol(r, true, CoinGold);
                    reels[c].SetCoinLabel(r, CoinLabel(board[c, r].Value));
                }
                else reels[c].HighlightSymbol(r, false);
            }
        ui.ShowWinAmount(running);
        yield return new WaitForSeconds(0.9f);

        // Play each respin: flourish the empty cells, then reveal any new coins.
        foreach (var step in hold.steps)
        {
            yield return StartCoroutine(RespinEmptyCells(board));

            bool jackpotThisStep = false;
            foreach (var pl in step.newCoins)
            {
                board[pl.col, pl.row] = pl.face;
                reels[pl.col].SetSymbol(pl.row, Def.coinSymbol);
                reels[pl.col].HighlightSymbol(pl.row, true, CoinGold);
                reels[pl.col].SetCoinLabel(pl.row, CoinLabel(pl.face));
                running += pl.face.value;
                if (pl.face.jackpot != JackpotTier.None) { ui.ShowJackpotAward(pl.face.jackpot); jackpotThisStep = true; }
            }

            ui.ShowWinAmount(running);
            AudioManager.PlayClick();
            if (jackpotThisStep) yield return new WaitForSeconds(0.9f);
            ui.UpdateRespins(step.respinsLeft);
            yield return new WaitForSeconds(step.newCoins.Count > 0 ? 0.5f : 0.35f);
        }

        if (hold.grandAwarded)
        {
            ui.ShowJackpotAward(JackpotTier.Grand);
            running += hold.grandValue;
            ui.ShowWinAmount(running);
            yield return new WaitForSeconds(1.2f);
        }
        ui.HideHoldBanner();

        long totalWin = baseWin + hold.totalWin;
        float m = (float)totalWin / Mathf.Max(1, bet);
        var tier = totalWin <= 0 ? WinCelebrationTier.None
            : m >= 50f ? WinCelebrationTier.EpicWin
            : m >= 25f ? WinCelebrationTier.MegaWin
            : m >= 10f ? WinCelebrationTier.BigWin
            : WinCelebrationTier.Normal;

        yield return StartCoroutine(FinishSpin(bet, totalWin, tier, false, 0));
    }

    // Brief spin flourish on the cells that have no coin locked, then leave them
    // dimmed. Purely cosmetic — the coins and payout come from the resolver.
    IEnumerator RespinEmptyCells(CoinFace?[,] board)
    {
        var strip = Def.reelStrip;
        int len = strip.Length;
        for (int t = 0; t < 4; t++)
        {
            for (int c = 0; c < Def.cols; c++)
                for (int r = 0; r < Def.rows; r++)
                    if (board[c, r] == null)
                        reels[c].SetSymbol(r, strip[UnityEngine.Random.Range(0, len)]);
            yield return new WaitForSeconds(0.06f);
        }
        for (int c = 0; c < Def.cols; c++)
            for (int r = 0; r < Def.rows; r++)
                if (board[c, r] == null)
                    reels[c].HighlightSymbol(r, false);
    }

    void HighlightAnywhereWins(SpinEvaluationResult eval)
    {
        for (int c = 0; c < Def.cols && c < reels.Length; c++)
            for (int r = 0; r < Def.rows; r++)
                reels[c].HighlightSymbol(r, false);

        var gold = new Color(1f, 0.85f, 0.2f, 1f);
        foreach (var win in eval.winningLines)
            foreach (var cd in win.coords)
                if (cd.col < reels.Length)
                    reels[cd.col].HighlightSymbol(cd.row, true, gold);
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

        // Hold & Win: sprinkle collector coins over the base grid. They overwrite
        // the base symbol (so they don't count toward an anywhere win) and 6+ of
        // them trigger the coin respin bonus. coinBaseLandChance is a Hold & Win
        // RTP knob tuned by Verify Slot Math.
        if (Def.payMode == PayMode.HoldAndWin)
        {
            for (int c = 0; c < Def.cols; c++)
                for (int r = 0; r < Def.rows; r++)
                    if (UnityEngine.Random.value < Def.coinBaseLandChance)
                        grid[c, r] = Def.coinSymbol;
        }

        // Nudge a share of spins into a guaranteed line win so the game stays
        // lively. The pool excludes top-award symbols on purpose. Pay-anywhere
        // games have no lines to force and lean on cascades instead.
        if (Def.payMode == PayMode.Paylines && Def.paylines != null &&
            UnityEngine.Random.value < Def.guaranteedWinChance)
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
