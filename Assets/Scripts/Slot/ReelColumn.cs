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

    public SymbolId[] currentVisible = new SymbolId[SlotDef.Rows];
    public bool isSpinning { get; private set; }

    float symbolHeight = 150f;
    float spinSpeed = 2200f; // px per sec

    public void Init(int colIdx, float symHeight, Func<SymbolId, Sprite> spriteGetter, Action<int> stopCallback)
    {
        columnIndex = colIdx;
        symbolHeight = symHeight;
        getSprite = spriteGetter;
        onReelStopped = stopCallback;

        // Set initial random symbols
        for (int r = 0; r < SlotDef.Rows; r++)
        {
            var sym = SlotDef.ReelStrip[UnityEngine.Random.Range(0, SlotDef.ReelStrip.Length)];
            SetSymbol(r, sym);
        }
    }

    public void SetSymbol(int row, SymbolId sym)
    {
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
            for (int r = 0; r < SlotDef.Rows; r++)
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
        for (int r = SlotDef.Rows - 1; r > 0; r--)
            SetSymbol(r, currentVisible[r - 1]);

        var newSym = SlotDef.ReelStrip[UnityEngine.Random.Range(0, SlotDef.ReelStrip.Length)];
        SetSymbol(0, newSym);
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
