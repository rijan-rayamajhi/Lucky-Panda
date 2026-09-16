using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : Popup
{
    public Button musicToggle;
    public Button sfxToggle;
    public Button hapticsToggle;

    public TMP_Text musicStatusText;
    public TMP_Text sfxStatusText;
    public TMP_Text hapticsStatusText;

    public Slider musicSlider;
    public Slider sfxSlider;

    void OnEnable()
    {
        RefreshUI();
    }

    void Start()
    {
        if (musicToggle) musicToggle.onClick.AddListener(OnToggleMusic);
        if (sfxToggle) sfxToggle.onClick.AddListener(OnToggleSfx);
        if (hapticsToggle) hapticsToggle.onClick.AddListener(OnToggleHaptics);
        if (musicSlider) musicSlider.onValueChanged.AddListener(v => { if (AudioManager.I != null) AudioManager.I.SetMusicVolume(v); });
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => { if (AudioManager.I != null) AudioManager.I.SetSfxVolume(v); });
        RefreshUI();
    }

    void OnToggleMusic()
    {
        if (AudioManager.I != null)
        {
            AudioManager.I.SetMusicEnabled(!AudioManager.I.musicEnabled);
            RefreshUI();
        }
    }

    void OnToggleSfx()
    {
        if (AudioManager.I != null)
        {
            AudioManager.I.SetSfxEnabled(!AudioManager.I.sfxEnabled);
            RefreshUI();
        }
    }

    void OnToggleHaptics()
    {
        if (AudioManager.I != null)
        {
            AudioManager.I.SetHapticsEnabled(!AudioManager.I.hapticsEnabled);
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        var am = AudioManager.I;
        bool musicOn = am == null || am.musicEnabled;
        bool sfxOn = am == null || am.sfxEnabled;
        bool hapticsOn = am == null || am.hapticsEnabled;

        ApplyToggleState(musicToggle, musicStatusText, musicOn);
        ApplyToggleState(sfxToggle, sfxStatusText, sfxOn);
        ApplyToggleState(hapticsToggle, hapticsStatusText, hapticsOn);

        if (musicSlider) musicSlider.SetValueWithoutNotify(am != null ? am.musicVolume : 0.65f);
        if (sfxSlider) sfxSlider.SetValueWithoutNotify(am != null ? am.sfxVolume : 1f);
    }

    void ApplyToggleState(Button btn, TMP_Text label, bool on)
    {
        if (label)
        {
            label.text = on ? "ON" : "OFF";
            label.color = on ? UIFactory.GoldBright : UIFactory.Dim;
        }

        if (btn)
        {
            UIFactory.SetPillState(btn, true, on ? "ON" : "OFF");
            var frame = btn.GetComponent<ThemedFrame>();
            if (frame != null)
            {
                if (on)
                {
                    frame.fillTop = UIFactory.GreenTop;
                    frame.fillBottom = UIFactory.GreenBottom;
                    frame.borderColor = UIFactory.GoldBright;
                }
                else
                {
                    frame.fillTop = new Color(0.14f, 0.10f, 0.20f, 1f);
                    frame.fillBottom = new Color(0.08f, 0.05f, 0.12f, 1f);
                    frame.borderColor = UIFactory.Dim;
                }
                frame.SetVerticesDirty();
            }
        }
    }
}
