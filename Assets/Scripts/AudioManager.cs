using UnityEngine;
using UnityEngine.UI;

/// Marker so a button is only ever hooked once, without a static registry that
/// would hold references to destroyed buttons forever.
public class ClickSound : MonoBehaviour { }

[DefaultExecutionOrder(-1000)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;

    [Header("Audio Clips")]
    public AudioClip clickSound;

    AudioSource sfxSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        if (I == null)
        {
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            I = go.AddComponent<AudioManager>();
        }
    }

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = gameObject.GetComponent<AudioSource>();
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D sound
        }

        if (clickSound == null)
            clickSound = Resources.Load<AudioClip>("click");
    }

    void Start()
    {
        HookScene();
    }

    /// Hooks every button currently in the scene. Called once at startup;
    /// panels hook their own rows as they build them.
    public static void HookScene()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            HookChildren(roots[i]);
    }

    // Replaces a Resources.FindObjectsOfTypeAll<Button>() sweep that ran every
    // 20 frames forever. That cost scaled with every button in memory, and a
    // card album plus a mail list adds hundreds.
    public static void HookChildren(GameObject root)
    {
        if (root == null) return;
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            HookButton(buttons[i]);
    }

    public static void HookButton(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<ClickSound>() != null) return;
        btn.gameObject.AddComponent<ClickSound>();
        btn.onClick.AddListener(PlayClick);
    }

    public static void PlayClick()
    {
        if (I != null) I.PlayClickInternal();
    }

    public void PlayClickInternal()
    {
        if (clickSound == null)
            clickSound = Resources.Load<AudioClip>("click");

        if (sfxSource != null && clickSound != null)
            sfxSource.PlayOneShot(clickSound, 1f);
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }
}
