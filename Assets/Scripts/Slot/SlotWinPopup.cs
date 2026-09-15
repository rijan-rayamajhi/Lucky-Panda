using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotWinPopup : MonoBehaviour
{
    public RectTransform card;
    public Image bannerImage;
    public TMP_Text titleText;
    public TMP_Text winAmountText;
    public Button collectButton;

    Action onDismiss;
    Coroutine countRoutine;

    public void Show(WinCelebrationTier tier, long amount, Action callback = null)
    {
        onDismiss = callback;
        gameObject.SetActive(true);

        switch (tier)
        {
            case WinCelebrationTier.EpicWin:
                titleText.text = "EPIC WIN!";
                titleText.color = new Color(1f, 0.85f, 0.2f);
                break;
            case WinCelebrationTier.MegaWin:
                titleText.text = "MEGA WIN!";
                titleText.color = new Color(0.95f, 0.45f, 1f);
                break;
            default:
                titleText.text = "BIG WIN!";
                titleText.color = new Color(1f, 0.6f, 0.2f);
                break;
        }

        if (countRoutine != null) StopCoroutine(countRoutine);
        countRoutine = StartCoroutine(CelebrationRoutine(amount));
    }

    IEnumerator CelebrationRoutine(long targetAmount)
    {
        // Punch scale card
        card.localScale = Vector3.zero;
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.25f;
            card.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        card.localScale = Vector3.one;

        // Roll up amount over 2 seconds
        float rollDuration = 2.0f;
        elapsed = 0f;
        var culture = CultureInfo.InvariantCulture;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollDuration;
            long cur = (long)Mathf.Lerp(0, targetAmount, t);
            winAmountText.text = cur.ToString("N0", culture);
            yield return null;
        }

        winAmountText.text = targetAmount.ToString("N0", culture);

        // Auto dismiss after 2 seconds or allow tap
        yield return new WaitForSeconds(2.0f);
        Dismiss();
    }

    public void Dismiss()
    {
        if (!gameObject.activeSelf) return;
        if (countRoutine != null) StopCoroutine(countRoutine);
        gameObject.SetActive(false);
        onDismiss?.Invoke();
    }
}
