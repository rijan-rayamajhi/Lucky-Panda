using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour
{
    public SlotMachine machine;

    [Header("Top HUD")]
    public TMP_Text coinText;
    public TMP_Text gemText;
    public Button backToLobbyBtn;
    public Button settingsBtn;
    public SettingsPanel settingsPanel;

    [Header("Jackpot Tickers")]
    public TMP_Text grandText;
    public TMP_Text majorText;
    public TMP_Text minorText;
    public TMP_Text miniText;

    [Header("Bottom Controls")]
    public Button spinBtn;
    public Button autoSpinBtn;
    public Image autoSpinIndicator;
    public Button betMinusBtn;
    public Button betPlusBtn;
    public Button maxBetBtn;
    public TMP_Text betAmountText;
    public TMP_Text winAmountText;

    [Header("Free Spins Banner")]
    public GameObject freeSpinsBanner;
    public TMP_Text freeSpinsCountText;

    [Header("Cascade Multiplier")]
    // Occupies the strip freed by hiding the jackpot row on cascade machines.
    public GameObject chainMultiplierRoot;
    public TMP_Text chainMultiplierText;

    [Header("Coin Gain Effect")]
    public Sprite coinIcon;
    const int FlyingCoinCount = 8;

    long displayedWin = 0;
    Coroutine winRollRoutine;
    Coroutine coinGainRoutine;

    // Progressive jackpot base amounts
    double grandPool = 1_250_000;
    double majorPool = 285_000;
    double minorPool = 65_000;
    double miniPool = 14_500;

    void Awake()
    {
        if (spinBtn) spinBtn.onClick.AddListener(OnSpinClicked);
        if (autoSpinBtn) autoSpinBtn.onClick.AddListener(OnAutoSpinClicked);
        if (betMinusBtn) betMinusBtn.onClick.AddListener(() => machine.ChangeBet(-1));
        if (betPlusBtn) betPlusBtn.onClick.AddListener(() => machine.ChangeBet(1));
        if (maxBetBtn) maxBetBtn.onClick.AddListener(() => machine.SetMaxBet());
        if (backToLobbyBtn) backToLobbyBtn.onClick.AddListener(OnBackToLobbyClicked);
        if (settingsBtn && settingsPanel) settingsBtn.onClick.AddListener(settingsPanel.Open);

        if (freeSpinsBanner) freeSpinsBanner.SetActive(false);
    }

    void Update()
    {
        // Keyboard hotkey for desktop/mac testing
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            OnSpinClicked();
        }
#else
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnSpinClicked();
        }
#endif

        // Slowly increment progressive jackpots
        grandPool += Time.deltaTime * 18.5;
        majorPool += Time.deltaTime * 8.2;
        minorPool += Time.deltaTime * 3.1;
        miniPool += Time.deltaTime * 1.2;

        var c = CultureInfo.InvariantCulture;
        if (grandText) grandText.text = ((long)grandPool).ToString("N0", c);
        if (majorText) majorText.text = ((long)majorPool).ToString("N0", c);
        if (minorText) minorText.text = ((long)minorPool).ToString("N0", c);
        if (miniText) miniText.text = ((long)miniPool).ToString("N0", c);
    }

    public void OnSpinClicked()
    {
        AudioManager.PlayClick();
        machine.TrySpin();
    }

    public void OnAutoSpinClicked()
    {
        machine.ToggleAutoSpin();
    }

    public void OnBackToLobbyClicked()
    {
        AudioManager.PlayClick();
        if (machine.IsAutoSpin) machine.StopAutoSpin();
        SceneManager.LoadScene("Lobby");
    }

    public void RefreshBalances()
    {
        var s = GameState.I;
        if (s == null) return;
        var c = CultureInfo.InvariantCulture;
        if (coinText) coinText.text = s.Data.coins.ToString("N0", c);
        if (gemText) gemText.text = s.Data.gems.ToString(c);
    }

    public void RefreshGemsOnly()
    {
        var s = GameState.I;
        if (s == null) return;
        if (gemText) gemText.text = s.Data.gems.ToString(CultureInfo.InvariantCulture);
    }

    // Coins fly from the win pill to the HUD balance while the balance text
    // counts up to match, so a win reads as coins landing rather than a jump.
    public void AnimateCoinGain(long from, long to)
    {
        if (coinGainRoutine != null) StopCoroutine(coinGainRoutine);
        if (to <= from || winAmountText == null || coinText == null)
        {
            if (coinText) coinText.text = to.ToString("N0", CultureInfo.InvariantCulture);
            return;
        }
        coinGainRoutine = StartCoroutine(CoinGainRoutine(from, to));
    }

    const float CoinFlightDuration = 0.6f;

    IEnumerator CoinGainRoutine(long start, long target)
    {
        // Runs for as long as the coin sound plays, so the count-up and the
        // last coins landing line up with the audio instead of finishing
        // while the ding is still ringing.
        var clip = AudioManager.I != null ? AudioManager.I.coinSound : null;
        float duration = clip != null ? clip.length : 0.9f;

        float stagger = Mathf.Max(0.05f, (duration - CoinFlightDuration) / FlyingCoinCount);
        for (int i = 0; i < FlyingCoinCount; i++)
            StartCoroutine(FlyOneCoin(winAmountText.rectTransform.position, coinText.rectTransform.position, i * stagger));

        float elapsed = 0f;
        var c = CultureInfo.InvariantCulture;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            long cur = (long)Mathf.Lerp(start, target, elapsed / duration);
            if (coinText) coinText.text = cur.ToString("N0", c);
            yield return null;
        }

        if (coinText) coinText.text = target.ToString("N0", c);
    }

    IEnumerator FlyOneCoin(Vector3 fromPos, Vector3 toPos, float delay)
    {
        if (coinIcon == null) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(44, 44);
        Vector3 start = fromPos + (Vector3)Random.insideUnitCircle * 30f;
        rt.position = start;

        var img = go.GetComponent<Image>();
        img.sprite = coinIcon;
        img.raycastTarget = false;
        img.preserveAspect = true;

        // Random arc peak so the coins scatter instead of travelling in a
        // dead-straight line to the balance pill.
        Vector3 arcOffset = new Vector3(Random.Range(-40f, 40f), Random.Range(40f, 90f), 0f);

        float duration = CoinFlightDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float ease = 1f - (1f - t) * (1f - t);
            rt.position = Vector3.Lerp(start, toPos, ease) + arcOffset * (4f * ease * (1f - ease));
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, ease);
            yield return null;
        }

        Destroy(go);
    }

    public void RefreshBet(long bet)
    {
        var c = CultureInfo.InvariantCulture;
        if (betAmountText) betAmountText.text = bet.ToString("N0", c);
    }

    public void SetSpinButtonInteractable(bool interactable)
    {
        if (spinBtn) spinBtn.interactable = interactable;
    }

    public void SetAutoSpinState(bool active)
    {
        if (autoSpinIndicator)
            autoSpinIndicator.color = active ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.5f);

        if (autoSpinBtn)
        {
            var txt = autoSpinBtn.GetComponentInChildren<TMP_Text>();
            if (txt)
            {
                txt.text = active ? "STOP" : "AUTO";
                txt.color = active ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.84f, 0.40f);
            }
        }
    }

    public void ClearWinDisplay()
    {
        displayedWin = 0;
        if (winRollRoutine != null) StopCoroutine(winRollRoutine);
        if (winAmountText)
        {
            winAmountText.text = "0";
            winAmountText.color = new Color(1f, 0.84f, 0.40f);
        }
    }

    public void ShowWinAmount(long amount)
    {
        if (winRollRoutine != null) StopCoroutine(winRollRoutine);
        winRollRoutine = StartCoroutine(RollWinTextRoutine(amount));
    }

    IEnumerator RollWinTextRoutine(long target)
    {
        float duration = 0.85f;
        float elapsed = 0f;
        var c = CultureInfo.InvariantCulture;
        long start = displayedWin;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            displayedWin = (long)Mathf.Lerp(start, target, t);
            if (winAmountText) winAmountText.text = displayedWin.ToString("N0", c);
            yield return null;
        }

        displayedWin = target;
        if (winAmountText) winAmountText.text = target.ToString("N0", c);
    }

    public void ShowFreeSpinsBanner(int totalSpins)
    {
        if (freeSpinsBanner)
        {
            freeSpinsBanner.SetActive(true);
            UpdateFreeSpins(totalSpins);
        }
    }

    // Cascade chain multiplier, shown where the jackpot row would be. A ×1 step
    // is the un-cascaded base, so it reads dimmer than the rising chain.
    public void ShowChainMultiplier(int mult)
    {
        if (chainMultiplierRoot) chainMultiplierRoot.SetActive(true);
        if (chainMultiplierText)
        {
            chainMultiplierText.text = "x" + mult;
            chainMultiplierText.color = mult > 1
                ? new Color(1f, 0.85f, 0.2f, 1f)
                : new Color(0.7f, 0.7f, 0.7f, 0.9f);
        }
    }

    public void HideChainMultiplier()
    {
        if (chainMultiplierText)
        {
            chainMultiplierText.text = "x1";
            chainMultiplierText.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        }
    }

    public void UpdateFreeSpins(int remaining)
    {
        if (freeSpinsCountText)
            freeSpinsCountText.text = $"FREE SPINS: {remaining}";

        if (remaining <= 0 && freeSpinsBanner)
            freeSpinsBanner.SetActive(false);
    }

    Coroutine insufficientFundsRoutine;

    public void ShowInsufficientFunds()
    {
        if (insufficientFundsRoutine != null) StopCoroutine(insufficientFundsRoutine);
        insufficientFundsRoutine = StartCoroutine(FlashInsufficientFundsRoutine());
    }

    IEnumerator FlashInsufficientFundsRoutine()
    {
        if (winAmountText == null) yield break;
        var prevColor = winAmountText.color;
        var prevText = winAmountText.text;

        winAmountText.color = new Color(1f, 0.35f, 0.35f, 1f);
        winAmountText.text = "NOT ENOUGH COINS!";

        yield return new WaitForSeconds(1.8f);

        winAmountText.color = prevColor;
        winAmountText.text = displayedWin > 0 ? displayedWin.ToString("N0", CultureInfo.InvariantCulture) : "0";
        insufficientFundsRoutine = null;
    }
}

