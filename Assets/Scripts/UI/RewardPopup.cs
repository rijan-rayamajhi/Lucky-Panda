using System.Collections;
using UnityEngine;
using TMPro;

// One celebration for every payout in the game, so a quest, a set, the wheel
// and a puzzle all land the same way. Lives on the Canvas so it is always
// active; `root` is the part that shows and hides.
public class RewardPopup : MonoBehaviour
{
    public GameObject root;
    public RectTransform card;
    public TMP_Text headline;
    public TMP_Text body;

    public float holdSeconds = 1.9f;

    Coroutine running;

    void OnEnable()
    {
        RewardService.Granted -= OnGranted;
        RewardService.Granted += OnGranted;
    }

    void OnDisable()
    {
        RewardService.Granted -= OnGranted;
    }

    void OnGranted(Reward reward, string title)
    {
        Show(string.IsNullOrEmpty(title) ? "YOU GOT" : title, reward.Describe());
    }

    public void Show(string title, string text)
    {
        if (root == null) return;
        if (headline) headline.text = title;
        if (body) body.text = text;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Play());
    }

    public void Dismiss()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        if (root) root.SetActive(false);
    }

    IEnumerator Play()
    {
        root.SetActive(true);
        if (card != null)
        {
            for (float t = 0f; t < 0.28f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.28f);
                card.localScale = Vector3.one * EaseOutBack(k);
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(holdSeconds);

        if (card != null)
        {
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.16f);
                card.localScale = Vector3.one * (1f - k);
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        root.SetActive(false);
        running = null;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float inv = x - 1f;
        return 1f + c3 * inv * inv * inv + c1 * inv * inv;
    }
}
