using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Daily bonus wheel. One free spin per 24h: spins with an ease-out, lands the
// chosen segment under the top pointer, grants the reward, and persists the
// claim time so the button locks until tomorrow.
public class WheelPanel : MonoBehaviour
{
    public RectTransform wheel;      // rotating face
    public Button spinButton;
    public TMP_Text status;
    public RectTransform pointer;    // flapper / pointer above wheel
    public long[] coinRewards;       // per segment, index-aligned
    public int[] gemRewards;

    public float spinSeconds = 3.8f;
    public int fullSpins = 5;
    public float sliceOffsetDeg = 0f; // Slices are centered at i * 45 deg (0 = top red slice)

    bool spinning;
    TMP_Text spinButtonText;
    Coroutine spinRoutine;
    Coroutine winPulseRoutine;

    void Awake()
    {
        if (spinButton)
        {
            spinButton.onClick.AddListener(Spin);
            spinButtonText = spinButton.GetComponentInChildren<TMP_Text>();
            if (spinButtonText != null)
            {
                spinButtonText.textWrappingMode = TextWrappingModes.NoWrap;
                spinButtonText.enableAutoSizing = true;
                spinButtonText.fontSizeMin = 20;
                spinButtonText.fontSizeMax = 42;
                spinButtonText.text = "SPIN";
            }
        }

        if (pointer == null)
        {
            var p = transform.Find("Content/Pointer") ?? transform.Find("Card/Pointer") ?? transform.Find("Pointer");
            if (p != null) pointer = p as RectTransform;
        }

        AlignSlices();
    }

    void OnEnable()
    {
        AlignSlices();
        if (wheel) wheel.localEulerAngles = new Vector3(0f, 0f, sliceOffsetDeg);
        spinning = false;
        Refresh();
    }

    void OnDisable()
    {
        // Closing the page mid-spin cleans up coroutine state so the button resets cleanly
        if (spinRoutine != null) StopCoroutine(spinRoutine);
        if (winPulseRoutine != null) StopCoroutine(winPulseRoutine);
        spinning = false;
        if (pointer) pointer.localEulerAngles = Vector3.zero;
    }

    // Ensures all segment labels are placed dead-center in their colored slice
    // regardless of whether the scene was freshly built or loaded from existing serialized YAML.
    public void AlignSlices()
    {
        sliceOffsetDeg = 0f;
        if (wheel == null) return;

        const float r = 150f;
        int count = coinRewards != null && coinRewards.Length > 0 ? coinRewards.Length : 8;

        for (int i = 0; i < count; i++)
        {
            var segT = wheel.Find("Seg" + i);
            if (segT is RectTransform rt)
            {
                // Angle 0 is at top (12 o'clock, x=0, y=r)
                float ang = i * Mathf.PI * 2f / count;
                rt.anchoredPosition = new Vector2(Mathf.Sin(ang) * r, Mathf.Cos(ang) * r);
                // Radiates outward so it reads right-side up under the top pointer
                rt.localRotation = Quaternion.Euler(0f, 0f, -i * 360f / count);
            }
        }
    }

    void Update()
    {
        if (spinning) return;

#if UNITY_EDITOR
        // Quick dev affordance: press 'R' while wheel is open to reset cooldown
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetCooldown();
            return;
        }
#else
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCooldown();
            return;
        }
#endif
#endif

        // Live countdown timer ticking down every frame
        bool can = CanClaim(out var wait);
        if (can)
        {
            if (spinButton && !spinButton.interactable) spinButton.interactable = true;
            if (status && !status.text.StartsWith("🎉") && !status.text.StartsWith("💎") && status.text != "SPIN TO WIN!")
            {
                status.text = "SPIN TO WIN!";
                status.color = new Color(1f, 0.85f, 0.35f);
            }
        }
        else
        {
            if (spinButton && spinButton.interactable) spinButton.interactable = false;

            if (status && !status.text.StartsWith("🎉") && !status.text.StartsWith("💎"))
            {
                string timeStr = wait.TotalHours >= 1
                    ? $"{wait.Hours:D2}h {wait.Minutes:D2}m {wait.Seconds:D2}s"
                    : $"{wait.Minutes:D2}m {wait.Seconds:D2}s";
                status.text = $"Next spin in {timeStr}";
                status.color = new Color(0.85f, 0.8f, 0.98f);
            }
        }
    }

    public bool CanClaim(out TimeSpan wait)
    {
        wait = TimeSpan.Zero;
        var s = GameState.I;
        if (s == null) return true;
        if (string.IsNullOrEmpty(s.Data.lastWheelUtc)) return true;
        if (!DateTime.TryParse(s.Data.lastWheelUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var last))
            return true;

        var next = last.ToUniversalTime().AddHours(24);
        var now = DateTime.UtcNow;
        if (now >= next) return true;
        wait = next - now;
        return false;
    }

    void Refresh()
    {
        bool can = CanClaim(out var wait);
        if (spinButton) spinButton.interactable = can && !spinning;
        if (spinButtonText && spinButtonText.text != "SPIN") spinButtonText.text = "SPIN";

        if (status)
        {
            if (spinning)
            {
                status.text = "Good luck!";
                status.color = Color.white;
            }
            else if (can)
            {
                status.text = "SPIN TO WIN!";
                status.color = new Color(1f, 0.85f, 0.35f);
            }
            else
            {
                string timeStr = wait.TotalHours >= 1
                    ? $"{wait.Hours:D2}h {wait.Minutes:D2}m {wait.Seconds:D2}s"
                    : $"{wait.Minutes:D2}m {wait.Seconds:D2}s";
                status.text = $"Next spin in {timeStr}";
                status.color = new Color(0.85f, 0.8f, 0.98f);
            }
        }
    }

    public void Spin()
    {
        if (spinning || coinRewards == null || coinRewards.Length == 0) return;
        if (!CanClaim(out _)) return;

        int chosenIndex = UnityEngine.Random.Range(0, coinRewards.Length);
        spinRoutine = StartCoroutine(SpinRoutine(chosenIndex));
    }

    IEnumerator SpinRoutine(int index)
    {
        spinning = true;
        if (spinButton) spinButton.interactable = false;
        if (status)
        {
            status.text = "Good luck!";
            status.color = Color.white;
        }

        int count = coinRewards.Length;
        float step = 360f / count;

        // In our coordinate system, slice index 0 is at top (0 deg).
        // Slices 1..7 are placed clockwise. Rotating the wheel CCW (+z)
        // by index * step brings slice `index` directly under the top pointer.
        float targetAngle = (index * step + sliceOffsetDeg) % 360f;
        float currentZ = wheel ? wheel.localEulerAngles.z : 0f;
        float currNorm = (currentZ % 360f + 360f) % 360f;

        // Forward spin delta to land exactly on targetAngle
        float forwardDelta = targetAngle - currNorm;
        if (forwardDelta <= 0f) forwardDelta += 360f;

        float totalDegrees = 360f * fullSpins + forwardDelta;
        float fromZ = currentZ;
        float toZ = currentZ + totalDegrees;

        float t = 0f;
        while (t < spinSeconds)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / spinSeconds);

            // Ease-out cubic for smooth natural deceleration
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            float z = Mathf.Lerp(fromZ, toZ, eased);

            if (wheel) wheel.localEulerAngles = new Vector3(0f, 0f, z);

            // Tactile pointer flapper wobble as dividers pass by
            if (pointer)
            {
                float speed = 1f - p;
                // Dividers pass every 45 deg, centered between slices
                float wobble = Mathf.Sin((z - (step * 0.5f)) * Mathf.Deg2Rad * count) * 14f * speed;
                pointer.localEulerAngles = new Vector3(0f, 0f, wobble);
            }

            yield return null;
        }

        // Snap to exact target
        if (wheel) wheel.localEulerAngles = new Vector3(0f, 0f, targetAngle);
        if (pointer)
        {
            // Tiny bounce on settling
            StartCoroutine(PointerSettleBounce());
        }

        Grant(index);
        spinning = false;
    }

    IEnumerator PointerSettleBounce()
    {
        if (pointer == null) yield break;
        float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / duration;
            float angle = Mathf.Sin(k * Mathf.PI * 3f) * 8f * (1f - k);
            pointer.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }
        pointer.localEulerAngles = Vector3.zero;
    }

    void Grant(int index)
    {
        var s = GameState.I;
        long coins = index < coinRewards.Length ? coinRewards[index] : 0;
        int gems = index < gemRewards.Length ? gemRewards[index] : 0;

        if (s != null)
        {
            s.Data.coins += coins;
            s.Data.gems += gems;
            s.Data.lastWheelUtc = DateTime.UtcNow.ToString("o");
            s.Save();
        }

        var lobby = FindFirstObjectByType<LobbyUI>();
        if (lobby) lobby.Refresh();

        string rewardStr = coins > 0
            ? coins.ToString("N0", CultureInfo.InvariantCulture) + " COINS"
            : gems + " GEMS";

        bool isJackpot = coins >= 100_000 || gems >= 50;
        if (status)
        {
            status.text = isJackpot
                ? $"🎉 JACKPOT! YOU WON {rewardStr}! 🎉"
                : $"✨ YOU WON {rewardStr}! ✨";
            status.color = new Color(1f, 0.90f, 0.35f);
        }

        // Pulse winning label & status text
        if (winPulseRoutine != null) StopCoroutine(winPulseRoutine);
        winPulseRoutine = StartCoroutine(CelebrateReward(index));
    }

    IEnumerator CelebrateReward(int index)
    {
        Transform winSeg = wheel ? wheel.Find("Seg" + index) : null;
        Vector3 origSegScale = winSeg ? winSeg.localScale : Vector3.one;
        Vector3 origStatusScale = status ? status.transform.localScale : Vector3.one;

        float duration = 1.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scaleBounce = 1f + Mathf.Sin(elapsed * Mathf.PI * 4f) * 0.2f;
            if (winSeg) winSeg.localScale = origSegScale * scaleBounce;
            if (status) status.transform.localScale = origStatusScale * (1f + Mathf.Sin(elapsed * Mathf.PI * 3f) * 0.1f);
            yield return null;
        }

        if (winSeg) winSeg.localScale = origSegScale;
        if (status) status.transform.localScale = origStatusScale;
    }

    [ContextMenu("Reset Cooldown (Instant Spin)")]
    public void ResetCooldown()
    {
        var s = GameState.I;
        if (s != null)
        {
            s.Data.lastWheelUtc = "";
            s.Save();
        }
        else
        {
            var json = PlayerPrefs.GetString(GameState.SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<PlayerData>(json);
                    if (data != null)
                    {
                        data.lastWheelUtc = "";
                        PlayerPrefs.SetString(GameState.SaveKey, JsonUtility.ToJson(data));
                        PlayerPrefs.Save();
                    }
                }
                catch { }
            }
        }

        if (spinRoutine != null) StopCoroutine(spinRoutine);
        spinning = false;
        Refresh();
        Debug.Log("<color=green>[WheelPanel]</color> Daily wheel cooldown reset! Spin ready.");
    }
}
