using System.Globalization;
using UnityEngine;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    public TMP_Text coinText;
    public TMP_Text gemText;
    public HostMessage host;
    public ProfilePanel profile;

    void Start()
    {
        Refresh();
        if (host) host.Say("WELCOME BACK! READY TO WIN BIG?");
    }

    public void Refresh()
    {
        var s = GameState.I;
        if (s == null) return;

        // Invariant, or device locale regroups this (en-IN gives "10,00,000").
        var c = CultureInfo.InvariantCulture;
        if (coinText) coinText.text = s.Data.coins.ToString("N0", c);
        if (gemText) gemText.text = s.Data.gems.ToString(c);
    }

    public void OnPlay()
    {
        // The slot game scene isn't built yet; don't throw until it exists.
        if (Application.CanStreamedLevelBeLoaded("SlotGame"))
            UnityEngine.SceneManagement.SceneManager.LoadScene("SlotGame");
        else
            Debug.Log("Slot game scene not built yet.");
    }
    public void OnBuy() => Debug.Log("Store not implemented");
    public void OnProfile() { if (profile) profile.Open(); }
}
