using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// First scene shown at launch. Displays the branded splash while it async-loads
// the Lobby, drives the progress bar, holds for a minimum so it never just
// flashes, then swaps to the Lobby.
public class LoadingScreen : MonoBehaviour
{
    public Image fillBar;
    public string nextScene = "Lobby";
    public float minSeconds = 1.6f;

    IEnumerator Start()
    {
        if (fillBar) fillBar.fillAmount = 0f;

        var op = SceneManager.LoadSceneAsync(nextScene);
        op.allowSceneActivation = false;

        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float load = Mathf.Clamp01(op.progress / 0.9f); // 0..0.9 until activation
            float time = Mathf.Clamp01(t / minSeconds);
            if (fillBar) fillBar.fillAmount = Mathf.Min(load, time);

            if (load >= 1f && t >= minSeconds)
            {
                if (fillBar) fillBar.fillAmount = 1f;
                op.allowSceneActivation = true;
                yield break;
            }
            yield return null;
        }
    }
}
