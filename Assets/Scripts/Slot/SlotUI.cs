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

    long displayedWin = 0;
    Coroutine winRollRoutine;

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

