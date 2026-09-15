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

    /// Takes an int rather than a SlotGameId so the lobby tiles can bake their
    /// game into the scene as a persistent listener argument.
    public void OnPlayGameIndex(int gameId)
    {
        var def = SlotCatalog.Get((SlotGameId)gameId);

        // Don't throw if that game's scene hasn't been built yet.
        if (Application.CanStreamedLevelBeLoaded(def.sceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(def.sceneName);
        else
            Debug.LogWarning($"Scene '{def.sceneName}' for {def.displayName} is not in the build settings yet.");
    }

    public void OnProfile() { if (profile) profile.Open(); }
}
