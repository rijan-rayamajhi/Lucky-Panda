using System.Collections;
using UnityEngine;
using TMPro;

// The host is a performance, not a transition. She anticipates, travels in,
// settles on a decaying spring, breathes while she talks, then leans before
// she leaves. Entry eases out, exit eases in; the bubble is secondary motion
// that lands after she does.
public class HostMessage : MonoBehaviour
{
    public RectTransform root;
    public RectTransform host;
    public RectTransform bubble;
    public TMP_Text bubbleText;

    [Header("Positions")]
    public float hiddenX = -900f;
    public float shownX = 0f;

    [Header("Timing")]
    public float anticipation = 0.16f;
    public float enterTime = 0.85f;
    public float bubbleTime = 0.45f;
    public float holdTime = 4f;
    public float bubbleCloseTime = 0.2f;
    public float exitLeanTime = 0.16f;
    public float exitTime = 0.45f;

    [Header("Feel")]
    public float enterScaleFrom = 0.88f;
    public float anticipationOffset = 70f;   // pulls further out before committing
    public float exitLeanOffset = 55f;       // leans in before leaving
    public float idleBob = 7f;               // vertical breathing, pixels
    public float idleTilt = 0.8f;            // degrees
    public float idleSpeed = 1.6f;

    Coroutine running;
    Coroutine idling;
    float hostBaseY;

    void Awake()
    {
        if (root == null) return;
        if (host != null) hostBaseY = host.anchoredPosition.y;
        root.anchoredPosition = new Vector2(hiddenX, root.anchoredPosition.y);
        if (bubble) bubble.localScale = Vector3.zero;
        root.gameObject.SetActive(false);
    }

    public void Say(string message)
    {
        if (root == null) return;
        if (running != null) StopCoroutine(running);
        if (bubbleText) bubbleText.text = message;
        running = StartCoroutine(Perform());
    }

    public void Dismiss()
    {
        // Exit() drives `root`; bail if there's nothing showing to dismiss.
        if (root == null || !root.gameObject.activeSelf) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Exit());
    }

    IEnumerator Perform()
    {
        root.gameObject.SetActive(true);
        if (bubble) bubble.localScale = Vector3.zero;
        root.localScale = Vector3.one * enterScaleFrom;

        // Anticipation: drift a little further off before committing inward.
        yield return Move(hiddenX, hiddenX - anticipationOffset, anticipation, EaseOutQuad);

        // Travel in and overshoot, then settle in decaying oscillations.
        yield return Enter(hiddenX - anticipationOffset, shownX, enterTime);

        StartIdle();
        yield return Pop(0f, 1f, bubbleTime, EaseOutElastic);
        yield return new WaitForSeconds(holdTime);
        yield return Exit();
    }

    IEnumerator Exit()
    {
        if (bubble && bubble.localScale.x > 0f)
            yield return Pop(bubble.localScale.x, 0f, bubbleCloseTime, EaseInQuad);

        StopIdle();

        // Lean in, then accelerate away — anticipation on the way out too.
        float x = root.anchoredPosition.x;
        yield return Move(x, x + exitLeanOffset, exitLeanTime, EaseOutQuad);
        yield return Move(root.anchoredPosition.x, hiddenX, exitTime, EaseInBack);

        root.gameObject.SetActive(false);
        running = null;
    }

    IEnumerator Enter(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float n = Mathf.Clamp01(t / duration);
            root.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, DampedSettle(n)),
                                                root.anchoredPosition.y);
            root.localScale = Vector3.one * Mathf.LerpUnclamped(enterScaleFrom, 1f, EaseOutCubic(n));
            yield return null;
        }
        root.anchoredPosition = new Vector2(to, root.anchoredPosition.y);
        root.localScale = Vector3.one;
    }

    IEnumerator Move(float from, float to, float duration, System.Func<float, float> ease)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = ease(Mathf.Clamp01(t / duration));
            root.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, k), root.anchoredPosition.y);
            yield return null;
        }
        root.anchoredPosition = new Vector2(to, root.anchoredPosition.y);
    }

    IEnumerator Pop(float from, float to, float duration, System.Func<float, float> ease)
    {
        if (bubble == null) yield break;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = ease(Mathf.Clamp01(t / duration));
            bubble.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, k);
            yield return null;
        }
        bubble.localScale = Vector3.one * to;
    }

    void StartIdle()
    {
        if (host == null || idling != null) return;
        idling = StartCoroutine(Idle());
    }

    void StopIdle()
    {
        if (idling == null) return;
        StopCoroutine(idling);
        idling = null;
        if (host == null) return;
        host.anchoredPosition = new Vector2(host.anchoredPosition.x, hostBaseY);
        host.localRotation = Quaternion.identity;
    }

    // Standing still reads as a dead sprite; a slow bob keeps her alive.
    IEnumerator Idle()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * idleSpeed;
            host.anchoredPosition = new Vector2(host.anchoredPosition.x,
                                                hostBaseY + Mathf.Sin(t) * idleBob);
            host.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * idleTilt);
            yield return null;
        }
    }

    // Spring that overshoots then rings down, rather than one soft bounce.
    static float DampedSettle(float x)
    {
        if (x >= 1f) return 1f;
        return 1f - Mathf.Exp(-6f * x) * Mathf.Cos(9f * x);
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    static float EaseInQuad(float x) => x * x;

    static float EaseInBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return c3 * x * x * x - c1 * x * x;
    }

    static float EaseOutElastic(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        const float c4 = 2f * Mathf.PI / 3f;
        return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * c4) + 1f;
    }
}
