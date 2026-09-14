using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InboxPanel : Popup
{
    public RectTransform listContent;
    public Button claimAllButton;
    public TMP_Text emptyLabel;

    const float RowHeight = 176f;

    void Awake()
    {
        if (claimAllButton) claimAllButton.onClick.AddListener(OnClaimAll);
    }

    void OnEnable()
    {
        MailService.Prune();
        Rebuild();
    }

    void OnClaimAll()
    {
        if (MailService.ClaimAll() > 0) Rebuild();
    }

    void Rebuild()
    {
        if (listContent == null) return;
        UIFactory.Clear(listContent);

        var mail = MailService.All;
        if (emptyLabel) emptyLabel.gameObject.SetActive(mail.Count == 0);

        for (int i = 0; i < mail.Count; i++)
            BuildRow(mail[i]);

        UIFactory.SetPillState(claimAllButton, MailService.UnclaimedCount > 0,
            MailService.UnclaimedCount > 0 ? "CLAIM ALL" : "NOTHING TO CLAIM");

        AudioManager.HookChildren(gameObject);
    }

    void BuildRow(MailEntry entry)
    {
        var row = UIFactory.Panel(listContent, "Mail_" + entry.id);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = RowHeight;
        le.minHeight = RowHeight;

        bool ready = entry.HasReward;

        row.sizeDelta = new Vector2(row.sizeDelta.x, RowHeight);
        var bg = UIFactory.RowPlate(row, ready, entry.claimed);

        // Gold spine positioned inside the inner purple plate, clearing the left ruby medallion.
        if (!entry.read)
        {
            var spine = UIFactory.Frame(row, "Unread", 4f, 0f, UIFactory.GoldBright,
                UIFactory.BarTop, UIFactory.BarBottom);
            Place(spine.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(6f, 100f));
        }

        // Title and body placed cleanly inside the inner purple area (x = 170f)
        var title = UIFactory.FitLabel(row, "Title", entry.title, 28, 18);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.color = entry.read ? UIFactory.Violet : UIFactory.Cream;
        UIFactory.Shadowed(title);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(170f, -16f), new Vector2(500f, 32f));

        // Sender right-aligned above the claim button, inside the right medallion boundary
        var sender = UIFactory.FitLabel(row, "Sender", entry.sender, 18, 12);
        sender.alignment = TextAlignmentOptions.MidlineRight;
        sender.color = UIFactory.Dim;
        Place(sender.rectTransform, new Vector2(1f, 1f), new Vector2(-165f, -16f), new Vector2(220f, 26f));

        var body = UIFactory.Label(row, "Body", entry.body, 19);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.color = UIFactory.Violet;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(170f, -54f), new Vector2(560f, 48f));

        if (!entry.reward.IsEmpty)
        {
            var chips = UIFactory.RewardChips(row, entry.reward, 30f);
            Place(chips, new Vector2(0f, 0f), new Vector2(170f, 18f), new Vector2(520f, 32f));
            var chipCanvas = chips.gameObject.AddComponent<CanvasGroup>();
            chipCanvas.alpha = entry.claimed ? 0.45f : 1f;

            // Claim button inset at -165f so it rests fully inside the plate, not on the right medallion
            var claim = UIFactory.PillButton(row, "Claim", entry.claimed ? "CLAIMED" : "CLAIM", new Vector2(180f, 60f));
            Place(claim.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-165f, -4f), new Vector2(180f, 60f));
            UIFactory.SetPillState(claim, !entry.claimed, entry.claimed ? "CLAIMED" : "CLAIM");

            var captured = entry;
            claim.onClick.AddListener(() =>
            {
                if (MailService.Claim(captured)) Rebuild();
            });
        }

        // Tapping the row marks it read.
        bg.raycastTarget = true;
        var rowButton = bg.gameObject.AddComponent<Button>();
        rowButton.transition = Selectable.Transition.None;
        var e = entry;
        rowButton.onClick.AddListener(() =>
        {
            if (!e.read) { MailService.MarkRead(e); Rebuild(); }
        });
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
