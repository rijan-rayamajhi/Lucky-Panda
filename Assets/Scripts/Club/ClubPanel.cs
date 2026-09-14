using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClubPanel : Popup
{
    public Image crest;
    public TMP_Text tierNameText;
    public TMP_Text pointsText;
    public ThemedFrame progressFill;
    public TMP_Text nextTierLabel;
    public RectTransform perksNowColumn;
    public RectTransform perksNextColumn;
    public RectTransform starRow;

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    void OnEnable() => Refresh();

    void Refresh()
    {
        var tier = ClubService.Current;
        var next = ClubService.Next;

        if (crest != null) crest.color = tier.crestColor;
        if (tierNameText != null)
        {
            tierNameText.text = tier.tierName;
            tierNameText.color = tier.crestColor;
        }

        if (pointsText != null)
        {
            pointsText.text = next == null
                ? ClubService.Points.ToString("N0", Inv) + " CLUB POINTS"
                : ClubService.Points.ToString("N0", Inv) + " / " + next.points.ToString("N0", Inv);
        }

        if (progressFill != null) progressFill.fill = ClubService.Progress01;

        if (nextTierLabel != null)
        {
            nextTierLabel.text = next == null
                ? "TOP TIER REACHED"
                : ClubService.PointsToNext.ToString("N0", Inv) + " POINTS TO " + next.tierName;
        }

        BuildStars(tier);
        BuildPerks(perksNowColumn, tier, false);
        BuildPerks(perksNextColumn, next, true);
    }

    void BuildStars(ClubTierDef tier)
    {
        if (starRow == null) return;
        UIFactory.Clear(starRow);

        int filled = ClubService.Tier + 1;
        for (int i = 0; i < Content.ClubTiers.Length; i++)
        {
            var star = UIFactory.Shape(starRow, "Star" + i, UIShape.Star,
                i < filled ? tier.crestColor : new Color(0.22f, 0.18f, 0.30f, 1f));
            var le = star.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 38f;
            le.preferredHeight = 38f;
        }
    }

    void BuildPerks(RectTransform column, ClubTierDef tier, bool isNext)
    {
        if (column == null) return;
        UIFactory.Clear(column);

        if (tier == null)
        {
            AddPerk(column, "NOTHING LEFT TO CLIMB", UIFactory.Violet);
            return;
        }

        AddPerk(column, Pct(tier.coinMultiplier) + " COIN WINS", UIFactory.Cream);
        AddPerk(column, "x" + tier.wheelMultiplier.ToString("0.##", Inv) + " DAILY WHEEL", UIFactory.Cream);
        AddPerk(column, tier.extraQuestSlots > 0
            ? "+" + tier.extraQuestSlots + " DAILY QUEST" + (tier.extraQuestSlots == 1 ? "" : "S")
            : "STANDARD QUESTS", tier.extraQuestSlots > 0 ? UIFactory.Cream : UIFactory.Dim);
        AddPerk(column, tier.dailyGemStipend > 0
            ? tier.dailyGemStipend + " GEMS EVERY DAY"
            : "NO GEM STIPEND", tier.dailyGemStipend > 0 ? UIFactory.Cream : UIFactory.Dim);
    }

    static string Pct(float multiplier)
    {
        int pct = Mathf.RoundToInt((multiplier - 1f) * 100f);
        return pct <= 0 ? "STANDARD" : "+" + pct + "%";
    }

    static void AddPerk(RectTransform column, string text, Color color)
    {
        var label = UIFactory.Label(column, "Perk", text, 24);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        var le = label.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
    }
}
