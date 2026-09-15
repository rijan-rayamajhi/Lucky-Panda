using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReelColumn : MonoBehaviour
{
    public int columnIndex;
    public RectTransform container;
    public Image[] symbolImages; // 3 visible rows
    public Func<SymbolId, Sprite> getSprite;
    public Action<int> onReelStopped;

    public SymbolId[] currentVisible;
    public bool isSpinning { get; private set; }

    float symbolHeight = 150f;
    int rows = 3;
    SymbolId[] reelStrip;

    public void Init(int colIdx, int rowCount, SymbolId[] strip, float symHeight,
                     Func<SymbolId, Sprite> spriteGetter, Action<int> stopCallback)
    {
        columnIndex = colIdx;
        rows = rowCount;
        reelStrip = strip;
        symbolHeight = symHeight;
        getSprite = spriteGetter;
        onReelStopped = stopCallback;

        currentVisible = new SymbolId[rows];

        // Set initial random symbols
        for (int r = 0; r < rows; r++)
            SetSymbol(r, RandomStripSymbol());
    }

    SymbolId RandomStripSymbol()
    {
        if (reelStrip == null || reelStrip.Length == 0) return SymbolId.Ten;
        return reelStrip[UnityEngine.Random.Range(0, reelStrip.Length)];
    }

    public void SetSymbol(int row, SymbolId sym)
    {
        if (currentVisible == null || row < 0 || row >= currentVisible.Length) return;
        currentVisible[row] = sym;
        if (symbolImages != null && row < symbolImages.Length && symbolImages[row] != null)
        {
            symbolImages[row].sprite = getSprite(sym);
            symbolImages[row].color = Color.white;
            symbolImages[row].transform.localScale = Vector3.one;
        }
    }

    public void StartSpin()
    {
        if (isSpinning) return;
        isSpinning = true;
        StartCoroutine(SpinRoutine());
    }

    SymbolId[] targetSymbols;
    bool stopRequested;

    public void RequestStop(SymbolId[] finalSymbols)
    {
        targetSymbols = finalSymbols;
        stopRequested = true;
    }

    IEnumerator SpinRoutine()
    {
        stopRequested = false;
        targetSymbols = null;

        // Initial anticipation jerk up
        float elapsed = 0f;
        Vector3 initialPos = container.anchoredPosition;
        while (elapsed < 0.12f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.12f;
            container.anchoredPosition = initialPos + new Vector3(0, Mathf.Sin(t * Mathf.PI) * 20f, 0);
            yield return null;
        }

        // Free continuous spin
        float stepTime = 0.045f;
        while (!stopRequested)
        {
            ShiftSymbolsRandom();
            yield return new WaitForSeconds(stepTime);
        }

        // Final sequence deceleration into target symbols
        for (int i = 0; i < 6; i++)
        {
            ShiftSymbolsRandom();
            yield return new WaitForSeconds(stepTime + i * 0.015f);
        }

        // Place final symbols
        if (targetSymbols != null)
        {
            for (int r = 0; r < rows && r < targetSymbols.Length; r++)
                SetSymbol(r, targetSymbols[r]);
        }

        // Settle bounce back
        elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.18f;
            // Overshoot down then spring back
            float bounce = Mathf.Sin(t * Mathf.PI) * -18f * (1f - t);
            container.anchoredPosition = initialPos + new Vector3(0, bounce, 0);
            yield return null;
        }

        container.anchoredPosition = initialPos;
        isSpinning = false;
        onReelStopped?.Invoke(columnIndex);
    }

    void ShiftSymbolsRandom()
    {
        if (currentVisible == null) return;
        for (int r = rows - 1; r > 0; r--)
            SetSymbol(r, currentVisible[r - 1]);

        SetSymbol(0, RandomStripSymbol());
    }

    /// Cascade collapse for this column: burst the flagged cells, then settle
    /// every row to `finalColumn` (top-to-bottom, as SlotCascade computed it)
    /// with a short drop. The final state must equal finalColumn — the pops and
    /// slide are cosmetic; correctness is that the grid matches the resolver.
    public IEnumerator BurstAndRefill(bool[] clearedRows, SymbolId[] finalColumn)
    {
        // Burst the winning cells.
        for (int r = 0; r < rows && r < clearedRows.Length; r++)
            if (clearedRows[r] && symbolImages != null && r < symbolImages.Length && symbolImages[r] != null)
                StartCoroutine(BurstOne(symbolImages[r].transform));
        yield return new WaitForSeconds(0.16f);

        // Drop in the resolved symbols.
        for (int r = 0; r < rows && r < finalColumn.Length; r++)
            SetSymbol(r, finalColumn[r]);

        // Short settle so the refill reads as falling, not snapping.
        Vector3 basePos = container.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < 0.16f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.16f;
            float drop = Mathf.Lerp(symbolHeight * 0.35f, 0f, t);
            container.anchoredPosition = basePos + new Vector3(0, drop, 0);
            yield return null;
        }
        container.anchoredPosition = basePos;
    }

    IEnumerator BurstOne(Transform tr)
    {
        Vector3 start = Vector3.one;
        float elapsed = 0f;
        while (elapsed < 0.16f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.16f;
            tr.localScale = Vector3.Lerp(start, new Vector3(1.25f, 1.25f, 1f) * 0.01f, t);
            yield return null;
        }
        tr.localScale = Vector3.one; // reset; SetSymbol will overwrite the sprite
    }

    public void HighlightSymbol(int row, bool highlight, Color? highlightColor = null)
    {
        if (row >= 0 && row < symbolImages.Length && symbolImages[row] != null)
        {
            var img = symbolImages[row];
            if (highlight)
            {
                img.color = Color.white;
                // Punch scale pulse
                StopCoroutine("PulseRoutine");
                StartCoroutine("PulseRoutine", img.transform);

                // Add or enable a glowing outline on the winning symbol
                var outline = img.GetComponent<Outline>();
                if (outline == null) outline = img.gameObject.AddComponent<Outline>();
                outline.enabled = true;
                outline.effectColor = highlightColor ?? new Color(1f, 0.85f, 0.2f, 0.95f);
                outline.effectDistance = new Vector2(4, 4);
            }
            else
            {
                img.color = new Color(0.35f, 0.35f, 0.35f, 0.65f);
                var outline = img.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                img.transform.localScale = Vector3.one;
            }
        }
    }

    public void ResetHighlights()
    {
        for (int r = 0; r < symbolImages.Length; r++)
        {
            if (symbolImages[r] != null)
            {
                symbolImages[r].color = Color.white;
                symbolImages[r].transform.localScale = Vector3.one;
                var outline = symbolImages[r].GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }
        }
    }

    IEnumerator PulseRoutine(Transform tr)
    {
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float s = 1f + Mathf.Sin((t / 0.6f) * Mathf.PI * 2f) * 0.18f;
            tr.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        tr.localScale = Vector3.one;
    }
}
