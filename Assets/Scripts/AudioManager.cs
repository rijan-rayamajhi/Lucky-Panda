using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;

    [Header("Audio Clips")]
    public AudioClip clickSound;

    AudioSource sfxSource;
    static readonly HashSet<Button> HookedButtons = new HashSet<Button>();

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
        {
            clickSound = Resources.Load<AudioClip>("click");
        }

        HookAllExistingButtons();
    }

    void Start()
    {
        HookAllExistingButtons();
    }

    void Update()
    {
        // Keep hooking any dynamically created or enabled buttons
        if (Time.frameCount % 20 == 0)
        {
            HookAllExistingButtons();
        }
    }

    public static void HookAllExistingButtons()
    {
        var buttons = Resources.FindObjectsOfTypeAll<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            HookButton(buttons[i]);
        }
    }

    public static void HookButton(Button btn)
    {
        if (btn == null) return;
        if (!HookedButtons.Contains(btn))
        {
            HookedButtons.Add(btn);
            btn.onClick.AddListener(PlayClick);
        }
    }

    public static void PlayClick()
    {
        if (I != null)
        {
            I.PlayClickInternal();
        }
    }

    public void PlayClickInternal()
    {
        if (clickSound == null)
        {
            clickSound = Resources.Load<AudioClip>("click");
        }

        if (sfxSource != null && clickSound != null)
        {
            sfxSource.PlayOneShot(clickSound, 1f);
        }
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}
