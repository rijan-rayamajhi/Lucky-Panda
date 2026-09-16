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
    public AudioClip musicClip;
    public AudioClip coinSound;
    public AudioClip spinSound;

    AudioSource sfxSource;
    AudioSource musicSource;
    AudioSource spinSource;

    const string KeyMusic = "LuckyPanda_MusicEnabled";
    const string KeySfx = "LuckyPanda_SfxEnabled";
    const string KeyHaptics = "LuckyPanda_HapticsEnabled";
    const string KeyMusicVol = "LuckyPanda_MusicVolume";
    const string KeySfxVol = "LuckyPanda_SfxVolume";

    public float musicVolume { get; private set; } = 0.65f;
    public float sfxVolume { get; private set; } = 1f;

    public bool musicEnabled { get; private set; } = true;
    public bool sfxEnabled { get; private set; } = true;
    public bool hapticsEnabled { get; private set; } = true;

    void Awake()
    {
        if (I != null && I != this)
        {
            if (musicClip != null)
            {
                I.musicClip = musicClip;
                I.PlayLobbyMusic();
            }
            if (clickSound != null)
            {
                I.clickSound = clickSound;
            }
            if (coinSound != null)
            {
                I.coinSound = coinSound;
            }
            if (spinSound != null)
            {
                I.spinSound = spinSound;
            }
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

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0.65f;

        // Dedicated source for the reel-spin whoosh (a one-shot at spin start),
        // separate so it can run under the reel-stop clicks and coin ding.
        spinSource = gameObject.AddComponent<AudioSource>();
        spinSource.playOnAwake = false;
        spinSource.loop = false;
        spinSource.spatialBlend = 0f;
        spinSource.volume = 1.0f;

        if (clickSound == null)
            clickSound = Resources.Load<AudioClip>("click");

        LoadPreferences();
    }

    void LoadPreferences()
    {
        musicEnabled = PlayerPrefs.GetInt(KeyMusic, 1) == 1;
        sfxEnabled = PlayerPrefs.GetInt(KeySfx, 1) == 1;
        hapticsEnabled = PlayerPrefs.GetInt(KeyHaptics, 1) == 1;
        musicVolume = PlayerPrefs.GetFloat(KeyMusicVol, 0.65f);
        sfxVolume = PlayerPrefs.GetFloat(KeySfxVol, 1f);
        if (musicSource != null) musicSource.volume = musicVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (spinSource != null) spinSource.volume = sfxVolume;

        if (musicSource != null) musicSource.mute = !musicEnabled;
        if (sfxSource != null) sfxSource.mute = !sfxEnabled;
        if (spinSource != null) spinSource.mute = !sfxEnabled;
    }

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        PlayerPrefs.SetInt(KeyMusic, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (musicSource != null)
        {
            musicSource.mute = !enabled;
            if (enabled && !musicSource.isPlaying && musicClip != null)
                musicSource.Play();
        }
    }

    public void SetSfxEnabled(bool enabled)
    {
        sfxEnabled = enabled;
        PlayerPrefs.SetInt(KeySfx, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (sfxSource != null)
            sfxSource.mute = !enabled;
        if (spinSource != null)
            spinSource.mute = !enabled;
    }

    public void SetHapticsEnabled(bool enabled)
    {
        hapticsEnabled = enabled;
        PlayerPrefs.SetInt(KeyHaptics, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// 0..1 volume controls a settings slider can bind to. On/off mutes still
    /// apply on top of these.
    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMusicVol, musicVolume);
        PlayerPrefs.Save();
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeySfxVol, sfxVolume);
        PlayerPrefs.Save();
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (spinSource != null) spinSource.volume = sfxVolume;
    }

    void Start()
    {
        HookScene();
        PlayLobbyMusic();
    }

    public void PlayLobbyMusic()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (musicClip != null)
        {
            musicSource.clip = musicClip;
            musicSource.volume = musicVolume;
            musicSource.loop = true;
            if (!musicSource.isPlaying)
                musicSource.Play();
        }
        else
        {
            Debug.LogWarning("AudioManager: musicClip is null!");
        }
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
        if (!sfxEnabled) return;
        if (clickSound == null)
            clickSound = Resources.Load<AudioClip>("click");

        if (sfxSource != null && clickSound != null)
            sfxSource.PlayOneShot(clickSound, 1f);
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (!sfxEnabled) return;
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    public static void PlaySpinSound()
    {
        if (I != null) I.PlaySpinSoundInternal();
    }

    public void PlaySpinSoundInternal()
    {
        if (spinSound == null)
            spinSound = Resources.Load<AudioClip>("spin");
        if (!sfxEnabled || spinSource == null || spinSound == null) return;
        spinSource.clip = spinSound;
        spinSource.time = 0f;
        spinSource.Play();
    }

    public static void StopSpinSound()
    {
        if (I != null && I.spinSource != null) I.spinSource.Stop();
    }

    public static void PlayCoinGain()
    {
        if (I != null) I.PlaySound(I.coinSound);
    }

    /// Coarse device vibration, gated by the Haptics setting. Previously the
    /// toggle saved a preference nothing ever read.
    public static void Haptic()
    {
        if (I == null || !I.hapticsEnabled) return;
#if UNITY_ANDROID || UNITY_IOS
        if (!Application.isEditor) Handheld.Vibrate();
#endif
    }

    /// Escalating win chime: one ding for a normal win, up to three staggered
    /// for an Epic, so the celebration is audibly bigger for bigger wins.
    public static void PlayWinCelebration(int dings)
    {
        if (I != null) I.StartCoroutine(I.WinCelebrationRoutine(dings));
    }

    System.Collections.IEnumerator WinCelebrationRoutine(int dings)
    {
        for (int i = 0; i < Mathf.Max(1, dings); i++)
        {
            PlaySound(coinSound, 1f);
            yield return new WaitForSeconds(0.16f);
        }
    }
}
