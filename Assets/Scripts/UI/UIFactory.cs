using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Runtime counterpart to the editor's LobbyBuilder helpers. Panels build their
// own rows here because row counts are data-driven — a mail list is however
// many messages there are, not a fixed set of slots baked into the scene.
public static class UIFactory
{
    public static readonly Color Gold = new Color(0.85f, 0.68f, 0.30f, 1f);
    public static readonly Color GoldBright = new Color(1f, 0.82f, 0.35f, 1f);
    public static readonly Color Cream = new Color(1f, 0.91f, 0.62f, 1f);
    public static readonly Color Violet = new Color(0.80f, 0.72f, 0.95f, 1f);
    // Deep plate that sits inside the painted popup frame. The frame art
    // stretches to a pale flat purple in the middle, which washes out anything
    // drawn on it; this gives the content a dark ground to read against, the
    // way the lobby's night sky does for its gold icons.
    public static readonly Color InnerTop = new Color(0.17f, 0.10f, 0.29f, 0.94f);
    public static readonly Color InnerBottom = new Color(0.06f, 0.03f, 0.12f, 0.97f);

    public static readonly Color RowFillTop = new Color(0.20f, 0.13f, 0.32f, 1f);
    public static readonly Color RowFillBottom = new Color(0.08f, 0.04f, 0.15f, 1f);

    /// Warmer plate for a row that has something to collect.
    public static readonly Color ReadyFillTop = new Color(0.30f, 0.19f, 0.16f, 1f);
    public static readonly Color ReadyFillBottom = new Color(0.14f, 0.07f, 0.10f, 1f);
    public static readonly Color TrackTop = new Color(0.04f, 0.02f, 0.08f, 0.92f);
    public static readonly Color TrackBottom = new Color(0.02f, 0.01f, 0.05f, 0.92f);
    public static readonly Color BarTop = new Color(1f, 0.92f, 0.45f, 1f);
    public static readonly Color BarBottom = new Color(0.96f, 0.64f, 0.16f, 1f);
    public static readonly Color GreenTop = new Color(0.16f, 0.42f, 0.12f, 1f);
    public static readonly Color GreenBottom = new Color(0.08f, 0.24f, 0.07f, 1f);
    public static readonly Color GemBlue = new Color(0.45f, 0.85f, 1f, 1f);
    public static readonly Color PillFillTop = new Color(0.12f, 0.09f, 0.16f, 0.96f);
    public static readonly Color PillFillBottom = new Color(0.05f, 0.03f, 0.08f, 0.96f);
    public static readonly Color Dim = new Color(0.35f, 0.30f, 0.45f, 1f);

    /// childControlWidth/Height default to FALSE on a freshly added layout
    /// group, which makes it position children without ever resizing them — so
    /// every child keeps RectTransform's default 100x100 and any LayoutElement
    /// is silently ignored. Always configure a group through here.
    public static T Configure<T>(T group, float spacing, TextAnchor align) where T : HorizontalOrVerticalLayoutGroup
    {
        group.spacing = spacing;
        group.childAlignment = align;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.childControlWidth = true;
        group.childControlHeight = true;
        return group;
    }

    public static RectTransform Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static TMP_Text Label(Transform parent, string name, string text, int size = 30)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        var font = ContentRefs.I != null ? ContentRefs.I.displayFont : null;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    /// Single-line label that shrinks to fit instead of disappearing.
    /// NoWrap combined with Ellipsis overflow can drop the string entirely in a
    /// fixed-width rect, which is what emptied the quest and mail row titles.
    public static TMP_Text FitLabel(Transform parent, string name, string text,
        int maxSize, int minSize = 16)
    {
        var t = Label(parent, name, text, maxSize);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        t.enableAutoSizing = true;
        t.fontSizeMin = minSize;
        t.fontSizeMax = maxSize;
        return t;
    }

    /// The lobby art carries its own drop shadows; flat UI text next to it
    /// reads as pasted on without one.
    public static TMP_Text Shadowed(TMP_Text text, float alpha = 0.8f, float distance = 2f)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = new Vector2(distance, -distance);
        return text;
    }

    public static Image Img(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (sprite == null) img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    public static ThemedFrame Frame(Transform parent, string name, float radius, float border,
        Color borderColor, Color fillTop, Color fillBottom)
    {
        var go = new GameObject(name, typeof(ThemedFrame));
        go.transform.SetParent(parent, false);
        var f = go.GetComponent<ThemedFrame>();
        var mat = ContentRefs.I != null ? ContentRefs.I.frameMaterial : null;
        if (mat != null) f.material = mat;
        f.cornerRadius = radius;
        f.borderThickness = border;
        f.borderColor = borderColor;
        f.fillTop = fillTop;
        f.fillBottom = fillBottom;
        f.raycastTarget = false;
        return f;
    }

    public static ShapeGraphic Shape(Transform parent, string name, UIShape shape, Color color)
    {
        var go = new GameObject(name, typeof(ShapeGraphic));
        go.transform.SetParent(parent, false);
        var s = go.GetComponent<ShapeGraphic>();
        s.shape = shape;
        s.color = color;
        s.raycastTarget = false;
        return s;
    }

    /// Source height of Btn_Gold.png / Row_Plate.png, used to scale their
    /// 9-slice borders so the ornament keeps its proportions at any height.
    public const float ButtonArtHeight = 562f;
    public const float RowArtHeight = 367f;

    public static readonly Color PlateDisabled = new Color(0.50f, 0.47f, 0.58f, 1f);

    /// Ornate button when the art is present, procedural pill when it is not.
    public static Button PillButton(Transform parent, string name, string text, Vector2 size)
    {
        var plate = ContentRefs.I != null ? ContentRefs.I.buttonPlate : null;
        Graphic graphic;

        if (plate != null)
        {
            var img = Img(parent, name, plate);
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.raycastTarget = true;
            img.pixelsPerUnitMultiplier = ButtonArtHeight / Mathf.Max(1f, size.y);
            img.rectTransform.sizeDelta = size;
            graphic = img;
        }
        else
        {
            var frame = Frame(parent, name, size.y * 0.5f, 3f, GoldBright, GreenTop, GreenBottom);
            frame.raycastTarget = true;
            frame.rectTransform.sizeDelta = size;
            graphic = frame;
        }

        var label = Label(graphic.transform, "Text", text, 28);
        Stretch(label.rectTransform);
        // Inside the painted end caps, which eat about 11% of the width each.
        label.rectTransform.offsetMin = new Vector2(size.x * 0.14f, 0f);
        label.rectTransform.offsetMax = new Vector2(-size.x * 0.14f, 0f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10;
        label.fontSizeMax = 30;
        // Even at fontSizeMin, a label longer than the plate's painted end
        // caps allow would otherwise spill past the pill's rounded border.
        label.overflowMode = TextOverflowModes.Ellipsis;
        Shadowed(label, 0.85f, 2f);

        var btn = graphic.gameObject.AddComponent<Button>();
        btn.targetGraphic = graphic;
        return btn;
    }

    public static void SetPillState(Button btn, bool enabled, string text)
    {
        if (btn == null) return;
        btn.interactable = enabled;

        var img = btn.GetComponent<Image>();
        if (img != null) img.color = enabled ? Color.white : PlateDisabled;

        var frame = btn.GetComponent<ThemedFrame>();
        if (frame != null)
        {
            frame.fillTop = enabled ? GreenTop : new Color(0.10f, 0.09f, 0.14f, 1f);
            frame.fillBottom = enabled ? GreenBottom : new Color(0.06f, 0.05f, 0.09f, 1f);
            frame.borderColor = enabled ? GoldBright : Dim;
            frame.SetVerticesDirty();
        }

        var label = btn.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = text;
            label.color = enabled ? Color.white : Violet;
        }
    }

    /// Ornate list-row plate, tinted by how much attention the row wants.
    /// Falls back to a ThemedFrame when the art is missing.
    public static Graphic RowPlate(RectTransform row, bool ready, bool spent)
    {
        var plate = ContentRefs.I != null ? ContentRefs.I.rowPlate : null;
        if (plate == null)
        {
            var frame = Frame(row, "Bg", 20f, 3f,
                spent ? Dim : (ready ? GoldBright : Gold),
                ready ? ReadyFillTop : RowFillTop,
                ready ? ReadyFillBottom : RowFillBottom);
            Stretch(frame.rectTransform);
            return frame;
        }

        var img = Img(row, "Bg", plate);
        img.type = Image.Type.Sliced;
        img.preserveAspect = false;
        img.pixelsPerUnitMultiplier = RowArtHeight / Mathf.Max(1f, row.sizeDelta.y > 0 ? row.sizeDelta.y : 132f);
        Stretch(img.rectTransform);
        // The plate is painted at full vibrance; dimming is what separates a
        // finished row from one still worth tapping.
        img.color = spent ? new Color(0.46f, 0.44f, 0.52f, 1f)
                          : (ready ? Color.white : new Color(0.74f, 0.72f, 0.82f, 1f));
        return img;
    }

    /// Horizontal progress bar: dark track with a gold fill.
    public static ThemedFrame Bar(Transform parent, string name, Vector2 size, out ThemedFrame fill)
    {
        var track = Frame(parent, name, size.y * 0.5f, 3f, Gold, TrackTop, TrackBottom);
        track.rectTransform.sizeDelta = size;
        fill = Frame(track.transform, "Fill", size.y * 0.5f, 0f, BarTop, BarTop, BarBottom);
        Inset(fill.rectTransform, 4f);
        return track;
    }

    /// Icon + amount, used wherever a reward is listed.
    public static RectTransform Chip(Transform parent, Sprite icon, string amount, Color tint, float height = 40f)
    {
        var row = Panel(parent, "Chip");
        Configure(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 4f, TextAnchor.MiddleLeft);
        // No ContentSizeFitter here: this sits inside another layout group, and
        // the two fight over the same rect. The group already reports a
        // preferred width from its children, which is what the parent needs.

        if (icon != null)
        {
            var img = Img(row, "Icon", icon);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = height;
            le.preferredHeight = height;
        }

        var text = Label(row, "Amount", amount, Mathf.RoundToInt(height * 0.72f));
        text.color = tint;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        var tle = text.gameObject.AddComponent<LayoutElement>();
        tle.preferredHeight = height;
        return row;
    }

    /// Lays a Reward out as a row of chips. Returns the container.
    public static RectTransform RewardChips(Transform parent, Reward reward, float height = 40f)
    {
        var row = Panel(parent, "Rewards");
        Configure(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 16f, TextAnchor.MiddleLeft);
        var fit = row.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var refs = ContentRefs.I;
        if (reward.coins > 0)
            Chip(row, refs != null ? refs.iconCoin : null,
                 reward.coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), Cream, height);
        if (reward.gems > 0)
            Chip(row, refs != null ? refs.iconGem : null, reward.gems.ToString(), new Color(0.45f, 0.85f, 1f), height);
        if (reward.cardPacks > 0)
            Chip(row, refs != null ? refs.iconGift : null, "x" + reward.cardPacks, Cream, height);
        if (reward.puzzlePieces > 0)
            PieceChip(row, reward.puzzlePieces, height);
        return row;
    }

    static void PieceChip(Transform parent, int count, float height)
    {
        var row = Panel(parent, "PieceChip");
        Configure(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 4f, TextAnchor.MiddleLeft);

        var piece = Shape(row, "Piece", UIShape.PuzzlePiece, new Color(0.55f, 0.85f, 1f));
        var le = piece.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = height;
        le.preferredHeight = height;

        var text = Label(row, "Amount", "x" + count, Mathf.RoundToInt(height * 0.72f));
        text.color = Cream;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    /// Vertical scrolling list. Returns the content rect that rows go into.
    public static RectTransform Scroll(RectTransform parent, string name, float spacing = 12f)
    {
        var root = Panel(parent, name);
        Stretch(root);

        var viewport = Panel(root, "Viewport");
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Panel(viewport, "Content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0, 0);
        content.offsetMax = new Vector2(0, 0);

        var layout = Configure(content.gameObject.AddComponent<VerticalLayoutGroup>(), spacing, TextAnchor.UpperCenter);
        layout.childForceExpandWidth = true;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void Inset(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// Painted 9-sliced procedural pill plate matching the Lobby standard;
    /// the 3D currency icon hangs off the left cap with drop shadows so it
    /// reads as sitting proudly above the plate rather than cramped inside it.
    public static TMP_Text CurrencyPill(Transform parent, string name, Sprite iconSprite, string value,
        Vector2 anchor, Vector2 pos, Vector2 size, Color rimColor)
    {
        var rim = Frame(parent, name, size.y * 0.5f, 10f, rimColor, PillFillTop, PillFillBottom);
        Place(rim.rectTransform, anchor, pos, size);

        var text = Label(rim.transform, "Value", value);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(size.y + 12f, 0f);
        rt.offsetMax = new Vector2(-size.y * 0.42f, 0f);
        text.alignment = TextAlignmentOptions.Midline;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18;
        text.fontSizeMax = 46;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        Shadowed(text, 0.8f, 2f);

        var icon = Img(rim.transform, "Icon", iconSprite);
        float d = size.y * 1.25f;
        Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(-d * 0.22f, 0f), new Vector2(d, d));
        var iconShadow = icon.gameObject.AddComponent<Shadow>();
        iconShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        iconShadow.effectDistance = new Vector2(3f, -4f);

        return text;
    }

    public static TMP_Text CurrencyPill(Transform parent, string name, Sprite iconSprite, string value,
        Vector2 pos, Vector2 size, Color rimColor)
    {
        return CurrencyPill(parent, name, iconSprite, value, new Vector2(0f, 0.5f), pos, size, rimColor);
    }
}

