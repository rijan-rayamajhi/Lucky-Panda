using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Piggy bank panel. Content is built by LobbyBuilder; this just refreshes the
// live numbers on open and handles the smash. Coin crediting, the coin sound
// and the lobby HUD update all happen inside GameState.AddCoins.
public class PiggyPanel : Popup
{
    public TMP_Text amountText;
    public TMP_Text hintText;
    public TMP_Text statusText;
    public Image fillBar;          // Image.Type.Filled, horizontal
    public Button smashButton;
    public TMP_Text smashLabel;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        var c = CultureInfo.InvariantCulture;
        long bal = PiggyService.Balance;
        bool full = PiggyService.IsFull;

        if (fillBar) fillBar.fillAmount = PiggyService.Fill01;
        if (amountText)
            amountText.text = bal.ToString("N0", c) + " / " + PiggyService.Capacity.ToString("N0", c);

        if (smashLabel)
            smashLabel.text = full ? "SMASH!" : $"SMASH • {PiggyService.EarlyBreakGemCost} GEMS";
        if (smashButton)
            smashButton.interactable = full || bal > 0;

        if (hintText)
            hintText.text = full
                ? "Your piggy is stuffed — smash it to collect!"
                : "Keep spinning to fatten your piggy: 5% of every bet drips in.";
    }

    public void OnSmashClicked()
    {
        AudioManager.PlayClick();
        bool wasFull = PiggyService.IsFull;
        long collected = wasFull ? PiggyService.Break() : PiggyService.BreakEarly();

        if (collected <= 0)
        {
            // Only reason a non-full smash returns 0 with a non-empty piggy is
            // not enough gems (an empty piggy leaves the button disabled).
            if (statusText && !wasFull && PiggyService.Balance > 0)
                statusText.text = "Not enough gems to smash early.";
            Refresh();
            return;
        }

        if (statusText)
            statusText.text = "SMASHED!  +" + collected.ToString("N0", CultureInfo.InvariantCulture) + " COINS";
        Refresh();
    }
}
