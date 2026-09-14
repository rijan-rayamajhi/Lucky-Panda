using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardsPanel : Popup
{
    public RectTransform tabsRow;
    public RectTransform grid;
    public TMP_Text setNameText;
    public TMP_Text countText;
    public ThemedFrame setProgressFill;
    public RectTransform setRewardRow;
    public Button claimSetButton;
    public Button openPackButton;

    [Header("Pack reveal")]
    public GameObject packOverlay;
    public RectTransform packCardRow;
    public TMP_Text packHeadline;
    public Button packDismissButton;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    int activeSet;
    Coroutine revealing;

    // 1..5 stars. Grey, green, blue, violet, gold.
    static readonly Color[] RarityColors =
    {
        new Color(0.45f, 0.42f, 0.55f),
        new Color(0.62f, 0.64f, 0.72f),
        new Color(0.45f, 0.82f, 0.55f),
        new Color(0.45f, 0.72f, 1f),
        new Color(0.76f, 0.55f, 1f),
        new Color(1f, 0.82f, 0.35f)
    };

    static Color RarityColor(int rarity) =>
        RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];

    void Awake()
    {
        if (claimSetButton) claimSetButton.onClick.AddListener(OnClaimSet);
        if (openPackButton) openPackButton.onClick.AddListener(OnOpenPack);
        if (packDismissButton) packDismissButton.onClick.AddListener(ClosePackOverlay);
    }

    void OnEnable()
    {
        if (packOverlay) packOverlay.SetActive(false);
        BuildTabs();
        Refresh();
    }

    void OnDisable()
    {
        if (revealing != null) { StopCoroutine(revealing); revealing = null; }
    }

    // ---- tabs ----------------------------------------------------------

    void BuildTabs()
    {
        if (tabsRow == null) return;
        UIFactory.Clear(tabsRow);

        for (int i = 0; i < Content.CardSets.Length; i++)
        {
            var set = Content.CardSets[i];
            bool active = i == activeSet;
            bool ready = CardService.SetClaimable(set.setId);

            var frame = UIFactory.Frame(tabsRow, "Tab" + i, 14f, 3f,
                ready ? UIFactory.GoldBright : (active ? UIFactory.Gold : UIFactory.Dim),
                active ? new Color(0.20f, 0.14f, 0.30f, 1f) : UIFactory.RowFillTop,
                active ? new Color(0.12f, 0.08f, 0.20f, 1f) : UIFactory.RowFillBottom);
            frame.raycastTarget = true;

            frame.rectTransform.sizeDelta = new Vector2(224f, 56f);
            var le = frame.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 224f;
            le.preferredHeight = 56f;

            var label = UIFactory.FitLabel(frame.transform, "Text", set.setName, 20, 12);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(216f, 26f));
            label.color = active ? UIFactory.Cream : UIFactory.Violet;
            if (active) UIFactory.Shadowed(label);

            var owned = UIFactory.Label(frame.transform, "Count",
                CardService.OwnedInSet(set.setId) + " / " + Content.CardsPerSet, 15);
            UIFactory.Place(owned.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(216f, 22f));
            owned.alignment = TextAlignmentOptions.Center;
            owned.color = ready ? UIFactory.GoldBright : (active ? UIFactory.Cream : UIFactory.Dim);

            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            int captured = i;
            btn.onClick.AddListener(() =>
            {
                activeSet = captured;
                BuildTabs();
                Refresh();
            });
        }
    }

    // ---- album ---------------------------------------------------------

    void Refresh()
    {
        var set = Content.CardSets[Mathf.Clamp(activeSet, 0, Content.CardSets.Length - 1)];
        int owned = CardService.OwnedInSet(set.setId);

        if (setNameText) setNameText.text = set.setName;
        if (countText) countText.text = owned + " / " + Content.CardsPerSet;
        if (setProgressFill) setProgressFill.fill = owned / (float)Content.CardsPerSet;

        if (setRewardRow)
        {
            UIFactory.Clear(setRewardRow);
            UIFactory.RewardChips(setRewardRow, set.reward, 34f);
        }

        bool claimable = CardService.SetClaimable(set.setId);
        UIFactory.SetPillState(claimSetButton, claimable,
            CardService.SetClaimed(set.setId) ? "SET CLAIMED" : (claimable ? "CLAIM SET" : "INCOMPLETE"));

        int packs = CardService.PackCount;
        UIFactory.SetPillState(openPackButton, packs > 0,
            packs > 0 ? "OPEN PACK (" + packs + ")" : "NO PACKS");

        BuildGrid(set.setId);
        AudioManager.HookChildren(gameObject);
    }

    void BuildGrid(int setId)
    {
        if (grid == null) return;
        UIFactory.Clear(grid);

        for (int slot = 0; slot < Content.CardsPerSet; slot++)
        {
            int cardId = setId * Content.CardsPerSet + slot;
            var def = Content.Card(cardId);
            if (def == null) continue;
            BuildFace(grid, def, CardService.Owns(cardId), CardService.CountOf(cardId));
        }
    }

    // Measured from Card_Frame.png: the transparent window inside the painted
    // border, and the red banner strip below it that carries the name.
    static readonly Vector2 WindowMin = new Vector2(0.108f, 0.173f);
    static readonly Vector2 WindowMax = new Vector2(0.890f, 0.852f);
    static readonly Vector2 BannerMin = new Vector2(0.170f, 0.062f);
    static readonly Vector2 BannerMax = new Vector2(0.830f, 0.168f);

    RectTransform BuildFace(Transform parent, CardDef def, bool owned, int count)
    {
        Color rarity = RarityColor(def.rarity);
        var art = ContentRefs.I != null ? ContentRefs.I.cardFrame : null;

        var root = UIFactory.Panel(parent, "Card" + def.id);

        // Contents live inside the frame's window; the border is drawn over the
        // top afterwards so its gold overlaps the fill cleanly.
        var window = UIFactory.Frame(root, "Fill", 8f, art != null ? 0f : 3f,
            art != null ? Color.clear : (owned ? rarity : new Color(0.48f, 0.42f, 0.58f, 1f)),
            owned ? new Color(0.17f, 0.11f, 0.26f, 1f) : new Color(0.13f, 0.09f, 0.19f, 1f),
            owned ? new Color(0.08f, 0.04f, 0.15f, 1f) : new Color(0.07f, 0.05f, 0.11f, 1f));
        var wr = window.rectTransform;
        wr.anchorMin = art != null ? WindowMin : Vector2.zero;
        wr.anchorMax = art != null ? WindowMax : Vector2.one;
        wr.offsetMin = Vector2.zero;
        wr.offsetMax = Vector2.zero;

        var symbol = UIFactory.Img(window.transform, "Symbol", ContentRefs.Symbol(def.symbolIndex));
        symbol.color = owned ? rarity : new Color(0.45f, 0.38f, 0.55f, 0.9f);
        var sr = symbol.rectTransform;
        sr.anchorMin = new Vector2(0.14f, 0.34f);
        sr.anchorMax = new Vector2(0.86f, 0.96f);
        sr.offsetMin = Vector2.zero;
        sr.offsetMax = Vector2.zero;

        var stars = UIFactory.Panel(window.transform, "Stars");
        stars.anchorMin = new Vector2(0.04f, 0.04f);
        stars.anchorMax = new Vector2(0.96f, 0.28f);
        stars.offsetMin = Vector2.zero;
        stars.offsetMax = Vector2.zero;
        UIFactory.Configure(stars.gameObject.AddComponent<HorizontalLayoutGroup>(), 3f, TextAnchor.MiddleCenter);

        for (int i = 0; i < def.rarity; i++)
        {
            var star = UIFactory.Shape(stars, "S" + i, UIShape.Star,
                owned ? UIFactory.GoldBright : new Color(0.38f, 0.32f, 0.48f, 1f));
            var sle = star.gameObject.AddComponent<LayoutElement>();
            sle.preferredWidth = 16f;
            sle.preferredHeight = 16f;
        }

        if (art != null)
        {
            var border = UIFactory.Img(root, "Frame", art);
            border.preserveAspect = false;
            border.color = owned ? Color.white : new Color(0.70f, 0.65f, 0.78f, 1f);
            UIFactory.Stretch(border.rectTransform);
        }

        // On the painted banner when the art is present, otherwise where the
        // old procedural face put it.
        var name = UIFactory.FitLabel(root, "Name", owned ? def.cardName : "???", 18, 9);
        var nr = name.rectTransform;
        nr.anchorMin = art != null ? BannerMin : new Vector2(0.08f, 0.08f);
        nr.anchorMax = art != null ? BannerMax : new Vector2(0.92f, 0.26f);
        nr.offsetMin = Vector2.zero;
        nr.offsetMax = Vector2.zero;
        name.color = owned ? UIFactory.Cream : new Color(0.68f, 0.62f, 0.78f, 1f);
        UIFactory.Shadowed(name);

        if (owned && count > 1)
        {
            var dupe = UIFactory.Label(root, "Count", "x" + count, 18);
            UIFactory.Place(dupe.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -4f), new Vector2(52f, 28f));
            dupe.color = UIFactory.GoldBright;
            dupe.alignment = TextAlignmentOptions.Center;
            UIFactory.Shadowed(dupe);
        }

        return root;
    }

    // ---- actions -------------------------------------------------------

    void OnClaimSet()
    {
        var set = Content.CardSets[Mathf.Clamp(activeSet, 0, Content.CardSets.Length - 1)];
        if (CardService.ClaimSet(set.setId))
        {
            BuildTabs();
            Refresh();
        }
    }

    void OnOpenPack()
    {
        var drops = CardService.OpenPack();
        if (drops == null || drops.Length == 0) return;

        if (revealing != null) StopCoroutine(revealing);
        revealing = StartCoroutine(Reveal(drops));
    }

    IEnumerator Reveal(CardDrop[] drops)
    {
        if (packOverlay == null || packCardRow == null)
        {
            BuildTabs();
            Refresh();
            yield break;
        }

        packOverlay.SetActive(true);
        UIFactory.Clear(packCardRow);
        if (packHeadline) packHeadline.text = "CARD PACK";

        // One frame so the cleared children are actually gone before rebuilding.
        yield return null;

        int newCards = 0;
        long dupeCoins = 0;

        for (int i = 0; i < drops.Length; i++)
        {
            var def = Content.Card(drops[i].cardId);
            if (def == null) continue;

            var face = BuildFace(packCardRow, def, true, 1);
            var le = face.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 204f;
            le.preferredHeight = 259f;

            if (drops[i].isNew)
            {
                newCards++;
                var ribbon = UIFactory.Label(face, "New", "NEW!", 22);
                UIFactory.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(120f, 32f));
                ribbon.color = UIFactory.GoldBright;
            }
            else if (drops[i].duplicateCoins > 0)
            {
                dupeCoins += drops[i].duplicateCoins;
                var dupe = UIFactory.Label(face, "Dupe",
                    "+" + drops[i].duplicateCoins.ToString("N0", Inv), 20);
                UIFactory.Place(dupe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(180f, 30f));
                dupe.color = UIFactory.Cream;
            }

            yield return PopIn(face, 0.26f);
            yield return new WaitForSeconds(0.12f);
        }

        if (packHeadline)
        {
            if (newCards > 0 && dupeCoins > 0)
                packHeadline.text = newCards + " NEW   ·   +" + dupeCoins.ToString("N0", Inv) + " COINS";
            else if (newCards > 0)
                packHeadline.text = newCards + (newCards == 1 ? " NEW CARD!" : " NEW CARDS!");
            else
                packHeadline.text = "+" + dupeCoins.ToString("N0", Inv) + " COINS FROM DUPLICATES";
        }

        revealing = null;
        BuildTabs();
        Refresh();
    }

    static IEnumerator PopIn(RectTransform rt, float duration)
    {
        rt.localScale = Vector3.zero;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float inv = k - 1f;
            rt.localScale = Vector3.one * (1f + c3 * inv * inv * inv + c1 * inv * inv);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    public void ClosePackOverlay()
    {
        if (revealing != null) { StopCoroutine(revealing); revealing = null; }
        if (packOverlay) packOverlay.SetActive(false);
        BuildTabs();
        Refresh();
    }
}
