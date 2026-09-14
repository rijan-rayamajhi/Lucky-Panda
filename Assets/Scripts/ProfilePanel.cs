using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProfilePanel : MonoBehaviour
{
    public GameObject root;
    public TMP_InputField nameField;
    public TMP_Text levelText;
    public TMP_Text xpText;
    public ThemedFrame xpFill;
    public TMP_Text spinsText;
    public TMP_Text wonText;
    public TMP_Text bestText;
    public TMP_Text sinceText;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    void Awake()
    {
        // No SetActive(false) here: `root` is this same GameObject and the scene
        // already saves it closed. Deactivating it during its own first
        // activation would immediately cancel Open().
        if (nameField) nameField.onEndEdit.AddListener(OnNameChanged);
    }

    public void Open()
    {
        if (root) root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (root) root.SetActive(false);
    }

    void OnNameChanged(string value)
    {
        var s = GameState.I;
        if (s == null) return;
        value = value.Trim();
        if (string.IsNullOrEmpty(value)) value = "PLAYER";
        s.Data.displayName = value;
        s.Save();
    }

    void Refresh()
    {
        var s = GameState.I;
        if (s == null) return;
        var d = s.Data;

        if (nameField) nameField.SetTextWithoutNotify(d.displayName);
        if (levelText) levelText.text = "LEVEL " + d.level.ToString(Inv);

        int need = s.XpForNextLevel;
        if (xpText) xpText.text = d.xp.ToString("N0", Inv) + " / " + need.ToString("N0", Inv);
        if (xpFill) xpFill.fill = need > 0 ? Mathf.Clamp01((float)d.xp / need) : 0f;

        if (spinsText) spinsText.text = d.totalSpins.ToString("N0", Inv);
        if (wonText) wonText.text = d.totalWon.ToString("N0", Inv);
        if (bestText) bestText.text = d.biggestWin.ToString("N0", Inv);

        if (sinceText)
        {
            sinceText.text = DateTime.TryParse(d.createdUtc, Inv,
                DateTimeStyles.RoundtripKind, out var created)
                ? created.ToLocalTime().ToString("dd MMM yyyy", Inv).ToUpperInvariant()
                : "-";
        }
    }
}
