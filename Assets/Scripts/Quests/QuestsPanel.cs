using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestsPanel : Popup
{
    public RectTransform listContent;
    public TMP_Text subtitle;

    const float RowHeight = 132f;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    void OnEnable()
    {
        QuestService.EnsureCurrent();
        Rebuild();
    }

    void Rebuild()
    {
        if (listContent == null) return;
        UIFactory.Clear(listContent);

        var ordered = new List<(QuestProgress p, bool weekly)>();
        Collect(ordered, QuestService.Daily, false);
        Collect(ordered, QuestService.Weekly, true);

        // Finished-but-unclaimed first: the thing worth tapping should not be
        // the thing you have to scroll for.
        ordered.Sort((a, b) =>
        {
            int ra = Rank(a.p), rb = Rank(b.p);
            if (ra != rb) return ra - rb;
            return a.weekly.CompareTo(b.weekly);
        });

        for (int i = 0; i < ordered.Count; i++)
            BuildRow(ordered[i].p, ordered[i].weekly);

        if (subtitle)
        {
            int claimable = QuestService.ClaimableCount;
            subtitle.text = claimable > 0
                ? claimable + (claimable == 1 ? " REWARD READY" : " REWARDS READY")
                : "RESETS DAILY";
            subtitle.color = claimable > 0 ? UIFactory.GoldBright : UIFactory.Violet;
        }

        AudioManager.HookChildren(gameObject);
    }

    static int Rank(QuestProgress p)
    {
        if (p.claimed) return 2;
        return QuestService.IsComplete(p) ? 0 : 1;
    }

    static void Collect(List<(QuestProgress, bool)> into, List<QuestProgress> list, bool weekly)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            if (Content.Quest(list[i].id) != null) into.Add((list[i], weekly));
    }

    void BuildRow(QuestProgress progress, bool weekly)
    {
        var def = Content.Quest(progress.id);
        if (def == null) return;

        bool complete = progress.progress >= def.target;

        var row = UIFactory.Panel(listContent, "Quest_" + def.id);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = RowHeight;
        le.minHeight = RowHeight;

        bool ready = complete && !progress.claimed;

        row.sizeDelta = new Vector2(row.sizeDelta.x, RowHeight);
        UIFactory.RowPlate(row, ready, progress.claimed);

        // Icon sitting cleanly inside the inner purple plate
        var disc = UIFactory.Shape(row, "Disc", UIShape.Circle,
            ready ? new Color(0.42f, 0.26f, 0.12f, 1f) : new Color(0.13f, 0.08f, 0.22f, 1f));
        Place(disc.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(64f, 64f));

        var star = UIFactory.Shape(row, "Icon", UIShape.Star,
            weekly ? UIFactory.GoldBright : (ready ? UIFactory.Cream : UIFactory.Violet));
        star.points = weekly ? 6 : 5;
        Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(46f, 46f));

        var title = UIFactory.FitLabel(row, "Title", def.title, 28, 16);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.color = progress.claimed ? UIFactory.Violet : UIFactory.Cream;
        UIFactory.Shadowed(title);
        float titleWidth = weekly ? 340f : 430f;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(230f, -16f), new Vector2(titleWidth, 36f));

        if (weekly)
        {
            var tagPlate = UIFactory.Frame(row, "Tag", 11f, 2f, UIFactory.GoldBright,
                new Color(0.40f, 0.26f, 0.06f, 1f), new Color(0.24f, 0.14f, 0.03f, 1f));
            Place(tagPlate.rectTransform, new Vector2(0f, 1f), new Vector2(230f + titleWidth + 10f, -16f), new Vector2(96f, 32f));
            var tag = UIFactory.Label(tagPlate.transform, "Text", "WEEKLY", 17);
            UIFactory.Stretch(tag.rectTransform);
            tag.color = UIFactory.GoldBright;
        }

        var track = UIFactory.Bar(row, "Bar", new Vector2(440f, 24f), out var fill);
        fill.fill = def.target > 0 ? Mathf.Clamp01(progress.progress / (float)def.target) : 0f;
        Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(230f, 22f), new Vector2(440f, 24f));

        var count = UIFactory.FitLabel(row, "Count",
            progress.progress.ToString("N0", Inv) + " / " + def.target.ToString("N0", Inv), 20, 13);
        count.alignment = TextAlignmentOptions.MidlineLeft;
        count.color = ready ? UIFactory.Cream : UIFactory.Violet;
        Place(count.rectTransform, new Vector2(0f, 0f), new Vector2(680f, 22f), new Vector2(160f, 26f));

        var chips = UIFactory.RewardChips(row, def.reward, 28f);
        Place(chips, new Vector2(1f, 1f), new Vector2(-165f, -16f), new Vector2(280f, 30f));
        var group = chips.gameObject.AddComponent<CanvasGroup>();
        group.alpha = progress.claimed ? 0.45f : 1f;

        var claim = UIFactory.PillButton(row, "Claim", "CLAIM", new Vector2(180f, 60f));
        Place(claim.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-165f, -6f), new Vector2(180f, 60f));
        UIFactory.SetPillState(claim, ready,
            progress.claimed ? "CLAIMED" : (complete ? "CLAIM" : "IN PROGRESS"));

        var captured = progress;
        claim.onClick.AddListener(() =>
        {
            if (QuestService.Claim(captured)) Rebuild();
        });
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
