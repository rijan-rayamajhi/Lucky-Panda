using UnityEngine;
using UnityEngine.UI;

// One purchasable pack in the shop. Mock purchase for now: clicking grants the
// coins/gems and persists — swap Buy() for real IAP later.
public class ShopPack : MonoBehaviour
{
    public long coins;
    public int gems;
    public Button button;

    void Awake()
    {
        if (button) button.onClick.AddListener(Buy);
    }

    public void Buy()
    {
        var s = GameState.I;
        if (s == null) return;

        // Shop source deliberately skips the club coin multiplier: a tier bonus
        // must not inflate currency the player paid money for.
        RewardService.Grant(Reward.Of(coins: coins, gems: gems), RewardSource.Shop, "PURCHASE COMPLETE");
        GameEvents.Raise(GameEventType.ShopPurchase, coins);
    }
}
