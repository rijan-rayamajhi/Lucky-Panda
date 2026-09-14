using System.Globalization;
using UnityEngine;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    public TMP_Text coinText;
    public TMP_Text gemText;
    public HostMessage host;
    public ProfilePanel profile;

    void OnEnable()
    {
        if (GameState.I == null) return;
        GameState.I.Changed -= Refresh;
        GameState.I.Changed += Refresh;
    }

    void OnDisable()
    {
        if (GameState.I != null) GameState.I.Changed -= Refresh;
    }

    void Start()
    {
        Refresh();
        if (host) host.Say(Greeting());
    }

    // The host is the only thing in the lobby that can point at the five
    // panels, so she says whichever one is actually worth opening.
    string Greeting()
    {
        if (QuestService.ClaimableCount > 0) return "YOUR QUESTS ARE READY TO CLAIM!";
        if (CardService.PackCount > 0) return "YOU HAVE CARD PACKS WAITING!";
        if (PuzzleService.IsComplete) return "YOUR PUZZLE IS COMPLETE — GO CLAIM IT!";
        if (MailService.UnclaimedCount > 0) return "THERE'S SOMETHING IN YOUR INBOX!";
        if (DailyService.WheelReady(out _)) return "YOUR DAILY SPIN IS READY!";
        return "WELCOME BACK! READY TO WIN BIG?";
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

    public void OnProfile() { if (profile) profile.Open(); }
}
