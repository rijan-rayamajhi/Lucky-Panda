using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class LobbyBuilder
{
    const string ArtUI = "Assets/Art/UI/";

    // Shared ThemedFrame palette (mirrors the values on the scene objects).
    static readonly Color Gold          = new Color(0.85f, 0.68f, 0.30f, 1f);
    static readonly Color GoldBright    = new Color(1f,    0.82f, 0.35f, 1f);
    static readonly Color GemBlue       = new Color(0.45f, 0.85f, 1f,    1f);
    static readonly Color PillFillTop   = new Color(0.12f, 0.09f, 0.16f, 0.96f);
    static readonly Color PillFillBottom= new Color(0.05f, 0.03f, 0.08f, 0.96f);
    static readonly Color TrackFillTop  = new Color(0.04f, 0.02f, 0.08f, 0.92f);
    static readonly Color TrackFillBottom = new Color(0.02f, 0.01f, 0.05f, 0.92f);
    static readonly Color XpFillTop     = new Color(1f,    0.92f, 0.45f, 1f);
    static readonly Color XpFillBottom  = new Color(0.96f, 0.64f, 0.16f, 1f);
    static readonly Color NameFillTop   = new Color(0.06f, 0.03f, 0.12f, 0.9f);
    static readonly Color NameFillBottom= new Color(0.03f, 0.015f,0.07f, 0.9f);
    const float BubbleSourceHeight = 1024f;
    const string ArtBG = "Assets/Art/Backgrounds/";
    const string ArtChar = "Assets/Art/Characters/";

    // Dev affordance: the wheel locks for 24h and currency only goes up, so
    // testing either needs a fresh save or a very patient tester.
    [MenuItem("Lucky Panda/Reset Player Save")]
    public static void ResetPlayerSave()
    {
        PlayerPrefs.DeleteKey(GameState.SaveKey);
        PlayerPrefs.Save();
        if (GameState.I != null)
        {
            GameState.I.Data = new PlayerData();
            GameState.I.Save();
        }
        Debug.Log("Player save cleared (coins, gems, level, wheel cooldown).");
    }

    [MenuItem("Lucky Panda/Reset Wheel Cooldown")]
    public static void ResetWheelCooldown()
    {
        if (GameState.I != null)
        {
            GameState.I.Data.lastWheelUtc = "";
            GameState.I.Save();
        }
        else
        {
            var json = PlayerPrefs.GetString(GameState.SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<PlayerData>(json);
                    if (data != null)
                    {
                        data.lastWheelUtc = "";
                        PlayerPrefs.SetString(GameState.SaveKey, JsonUtility.ToJson(data));
                        PlayerPrefs.Save();
                    }
                }
                catch { }
            }
        }
        var wp = Object.FindFirstObjectByType<WheelPanel>();
        if (wp != null) wp.ResetCooldown();
        Debug.Log("Daily wheel cooldown reset. Ready to spin!");
    }

    [MenuItem("Lucky Panda/Build Lobby Scene")]
    public static void Build()
    {
        // Art dropped in outside the editor is not in the database yet; without
        // this every sprite lookup below silently returns null.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Textures left in Multiple sprite mode resolve to an auto-slice scrap
        // when looked up by path, which is where the stray UI fragments came
        // from. Repair the import mode before anything loads a sprite.
        SpriteImportOptimizer.EnsureSingleSpriteMode();

        // Only sprites still drawn 9-sliced need borders. The pills, XP bar and
        // name plate are procedural ThemedFrames now, so re-importing their old
        // PNGs on every build was pure cost.
        // Left/bottom borders must enclose the tail so slicing never cuts it.
        SetSliceBorder(ArtUI + "Bubble_Speech.png", new Vector4(420, 340, 190, 190));
        // Even border all round, so one value covers every side.
        SetSliceBorder(ArtUI + "Panel_Popup2.png", new Vector4(150, 150, 150, 150));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

        new GameObject("GameState", typeof(GameState));
        new GameObject("AudioManager", typeof(AudioManager));

        // Background
        var bg = Img(canvasGO.transform, "Background", ArtBG + "BG_Lobby.png");
        bg.preserveAspect = false;
        Stretch(bg.rectTransform);

        // HUD and nav live inside the safe area; the background stays full-bleed.
        var safe = Panel(canvasGO.transform, "SafeArea");
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        // Top HUD
        var hud = Panel(safe, "TopHUD");
        Anchor(hud, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        hud.sizeDelta = new Vector2(0, 120);
        hud.anchoredPosition = Vector2.zero;

        // Profile avatar leads the HUD row, out of the host's way on the left.
        // Icon ships with its own gold ring, so no extra frame around it.
        var frame = Img(hud, "AvatarBadge", ArtUI + "Icon_Profile.png");
        Place(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(112, 112));

        // Pills start clear of the badge; each pill's icon overhangs ~23px left.
        var gemText = CurrencyPill(hud, "GemPill", ArtUI + "Icon_Gem.png", "150",
            new Vector2(184, 0), new Vector2(240, 84), GemBlue);
        var coinText = CurrencyPill(hud, "CoinPill", ArtUI + "Icon_Coin.png", "1,000,000",
            new Vector2(464, 0), new Vector2(380, 84), GoldBright);

        // Placeholder: invisible hit area, no art/label (real BUY button TBD).
        var buy = HitButton(hud, "BuyButton");
        Place(buy.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(220, 100));

        var piggy = Img(hud, "PiggyIcon", ArtUI + "Icon_Piggy.png");
        Place(piggy.rectTransform, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(90, 90));

        // Bottom nav
        var nav = Panel(safe, "BottomNav");
        Anchor(nav, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        nav.sizeDelta = new Vector2(0, 160);
        nav.anchoredPosition = Vector2.zero;
        var layout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 40;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        string[] navIcons  = { "Nav_Cards", "Nav_Puzzle", "Nav_Quest", "Nav_Wheel", "Nav_Inbox", "Nav_Club" };
        string[] navTitles = { "CARDS",     "PUZZLE",     "QUESTS",    "WHEEL",     "INBOX",     "CLUB"     };
        var navButtons = new Button[navIcons.Length];
        for (int k = 0; k < navIcons.Length; k++)
        {
            var i = Img(nav, navIcons[k], ArtUI + navIcons[k] + ".png");
            i.rectTransform.sizeDelta = new Vector2(120, 120);
            i.raycastTarget = true;
            var le = i.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 120; le.preferredHeight = 120;
            navButtons[k] = i.gameObject.AddComponent<Button>();
            navButtons[k].targetGraphic = i;
        }

        // Play button (center)
        // Placeholder: invisible hit area, no art/label (real SPIN button TBD).
        var play = HitButton(canvasGO.transform, "PlayButton");
        var playRT = play.GetComponent<RectTransform>();
        Place(playRT, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 240));
        // Host message group: hidden offscreen, slides in only when she speaks.
        var msgRoot = Panel(canvasGO.transform, "HostMessage");
        msgRoot.anchorMin = msgRoot.anchorMax = msgRoot.pivot = new Vector2(0, 0);
        msgRoot.sizeDelta = new Vector2(1200, 760);
        msgRoot.anchoredPosition = new Vector2(-900, 60);

        var hero = Img(msgRoot, "Host", ArtChar + "Host_Hero.png");
        // Pivot at her feet so the idle tilt reads as a weight shift, not a skew.
        Place(hero.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(470, 705));
        var heroRT = hero.rectTransform;
        heroRT.anchorMin = heroRT.anchorMax = new Vector2(0f, 0f);
        heroRT.pivot = new Vector2(0.5f, 0f);
        heroRT.anchoredPosition = new Vector2(275, 0);

        var bubbleSize = new Vector2(700, 520);
        var bubble = Img(msgRoot, "Bubble", ArtUI + "Bubble_Speech.png");
        bubble.type = Image.Type.Sliced;
        bubble.preserveAspect = false;
        // Same trick as the pill: keep the painted corners and the tail intact
        // while the middle stretches.
        bubble.pixelsPerUnitMultiplier = BubbleSourceHeight / bubbleSize.y;
        Place(bubble.rectTransform, new Vector2(0, 0), new Vector2(415, 360), bubbleSize);

        var bubbleText = Label(bubble.transform, "Text", "WELCOME BACK!");
        bubbleText.color = new Color(0.45f, 0.09f, 0.06f);
        bubbleText.fontSize = 46;
        bubbleText.enableAutoSizing = true;
        bubbleText.fontSizeMin = 24;
        bubbleText.fontSizeMax = 52;
        bubbleText.lineSpacing = -12f;
        var btRT = bubbleText.rectTransform;
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.offsetMin = new Vector2(30, 24);
        btRT.offsetMax = new Vector2(-30, -24);

        // Close button to dismiss the host message.
        var bubbleClose = Btn(bubble.transform, "BubbleClose", ArtUI + "Btn_Close.png");
        Place(bubbleClose.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-18, -20), new Vector2(72, 72));

        var hostMsg = canvasGO.AddComponent<HostMessage>();
        hostMsg.root = msgRoot;
        hostMsg.host = hero.rectTransform;
        hostMsg.bubble = bubble.rectTransform;
        hostMsg.bubbleText = bubbleText;
        OnClick(bubbleClose, hostMsg.Dismiss);
        msgRoot.gameObject.SetActive(false);

        var profile = BuildProfilePanel(canvasGO.transform);

        // Bottom-nav destination pages.
        for (int k = 0; k < navIcons.Length; k++)
        {
            Popup page = navTitles[k] == "WHEEL"
                ? BuildWheelPanel(canvasGO.transform)
                : BuildStubPanel(canvasGO.transform, navTitles[k] + "Panel", navTitles[k]);
            OnClick(navButtons[k], page.Open);
        }

        // Shop page opened by the BUY button.
        var shop = BuildShopPanel(canvasGO.transform);

        // Wire LobbyUI
        var ui = canvasGO.AddComponent<LobbyUI>();
        ui.coinText = coinText;
        ui.gemText = gemText;
        ui.host = hostMsg;
        ui.profile = profile;

        var badgeButton = frame.gameObject.AddComponent<Button>();
        badgeButton.targetGraphic = frame;
        frame.raycastTarget = true;
        OnClick(badgeButton, ui.OnProfile);
        OnClick(play, ui.OnPlay);
        OnClick(buy, shop.Open);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Lobby.unity");
        Debug.Log("Lobby scene built.");
    }

    // Painted 9-sliced pill plate; the icon hangs off the left cap so it
    // reads as sitting above the plate rather than inside it.
    static TMP_Text CurrencyPill(Transform parent, string name, string iconPath, string value,
        Vector2 pos, Vector2 size, Color rimColor)
    {
        // Procedural pill plate (rounded-rect frame) instead of a shared PNG,
        // so gem/coin differ in tint and stay crisp at any size.
        var rim = Frame(parent, name, size.y * 0.5f, 10f, rimColor, PillFillTop, PillFillBottom);
        Place(rim.rectTransform, new Vector2(0, 0.5f), pos, size);

        var text = Label(rim.transform, "Value", value);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(size.y + 12, 0);
        rt.offsetMax = new Vector2(-size.y * 0.42f, 0);
        text.alignment = TextAlignmentOptions.Midline;
        // Balances grow unbounded, so let the number shrink to fit the plate
        // instead of spilling past the gold cap.
        text.enableAutoSizing = true;
        text.fontSizeMin = 18;
        text.fontSizeMax = 46;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        var textShadow = text.gameObject.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0, 0, 0, 0.8f);
        textShadow.effectDistance = new Vector2(2, -2);

        var icon = Img(rim.transform, "Icon", iconPath);
        float d = size.y * 1.25f;
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(-d * 0.22f, 0), new Vector2(d, d));
        var iconShadow = icon.gameObject.AddComponent<Shadow>();
        iconShadow.effectColor = new Color(0, 0, 0, 0.65f);
        iconShadow.effectDistance = new Vector2(3, -4);

        return text;
    }

    static ProfilePanel BuildProfilePanel(Transform parent)
    {
        var root = Panel(parent, "ProfilePanel");
        Stretch(root);

        // Full-screen scrim doubles as tap-to-close.
        var scrim = Img(root, "Scrim", null);
        scrim.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrim.color = new Color(0f, 0f, 0f, 0.72f);
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);

        var cardSize = new Vector2(960, 720);
        var card = Img(root, "Card", ArtUI + "Panel_Popup2.png");
        card.type = Image.Type.Sliced;
        card.preserveAspect = false;
        card.raycastTarget = true;
        card.pixelsPerUnitMultiplier = 1086f / cardSize.y * 1.8f;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), cardSize);

        // Uniform border, so the inset is a thin even margin rather than a
        // large one sized to dodge corner scrollwork.
        var content = Panel(card.transform, "Content");
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(58, 36);
        content.offsetMax = new Vector2(-58, -96);

        // Reuse the ornate jackpot banner as the title plate.
        var title = Img(card.transform, "TitleBar", ArtUI + "Bar_Major.png");
        title.preserveAspect = true;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 65), new Vector2(540, 165));
        var titleText = Label(title.transform, "Text", "PROFILE");
        Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(420, 80));
        titleText.fontSize = 50;

        // Avatar
        var avatar = Img(content, "Avatar", ArtUI + "Icon_Profile.png");
        Place(avatar.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(148, 148));

        // Editable display name
        var nameField = BuildInput(content, "NameField",
            new Vector2(172, 0), new Vector2(560, 64));

        var levelText = Label(content, "LevelText", "LEVEL 1");
        Place(levelText.rectTransform, new Vector2(0, 1), new Vector2(172, -74), new Vector2(300, 36));
        levelText.alignment = TextAlignmentOptions.Left;
        levelText.fontSize = 32;

        // XP bar
        var xpSize = new Vector2(560, 48);
        var xpBg = Frame(content, "XpBg", xpSize.y * 0.5f, 6f, Gold, TrackFillTop, TrackFillBottom);
        Place(xpBg.rectTransform, new Vector2(0, 1), new Vector2(172, -118), xpSize);

        var xpFill = Frame(xpBg.transform, "XpFill", xpSize.y * 0.5f, 0f,
            XpFillTop, XpFillTop, XpFillBottom);
        xpFill.fill = 0.35f;
        Inset(xpFill.rectTransform, 6);

        var xpText = Label(xpBg.transform, "XpText", "0 / 1,000");
        Stretch(xpText.rectTransform);
        xpText.fontSize = 26;

        // Lifetime stats
        // Divider separates identity from statistics.
        Divider(content, "HeaderRule", -186, 0.55f);

        const float firstRow = -224f, rowStep = 68f;
        var spinsText = StatRow(content, "Spins", "TOTAL SPINS", firstRow);
        var wonText = StatRow(content, "Won", "TOTAL WON", firstRow - rowStep);
        var bestText = StatRow(content, "Best", "BIGGEST WIN", firstRow - rowStep * 2);
        var sinceText = StatRow(content, "Since", "PLAYING SINCE", firstRow - rowStep * 3);

        // Hairlines between rows so the block reads as a table, not a list.
        for (int i = 1; i <= 3; i++)
            Divider(content, "Rule" + i, firstRow - rowStep * i + rowStep * 0.5f - 4f, 0.22f);

        var close = Btn(card.transform, "CloseButton", ArtUI + "Btn_Close.png");
        Place(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -50), new Vector2(80, 80));

        var panel = root.gameObject.AddComponent<ProfilePanel>();
        panel.root = root.gameObject;
        panel.nameField = nameField;
        panel.levelText = levelText;
        panel.xpText = xpText;
        panel.xpFill = xpFill;
        panel.spinsText = spinsText;
        panel.wonText = wonText;
        panel.bestText = bestText;
        panel.sinceText = sinceText;

        OnClick(close, panel.Close);
        OnClick(scrim.gameObject.AddComponent<Button>(), panel.Close);

        root.gameObject.SetActive(false);
        return panel;
    }

    static TMP_Text StatRow(Transform parent, string name, string label, float y)
    {
        var row = Label(parent, name + "Label", label);
        Place(row.rectTransform, new Vector2(0, 1), new Vector2(6, y), new Vector2(420, 48));
        row.alignment = TextAlignmentOptions.MidlineLeft;
        row.fontSize = 30;
        row.color = new Color(0.80f, 0.72f, 0.95f);

        var value = Label(parent, name + "Value", "0");
        Place(value.rectTransform, new Vector2(1, 1), new Vector2(-6, y), new Vector2(360, 48));
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.fontSize = 36;
        value.color = new Color(1f, 0.91f, 0.62f);
        return value;
    }

    // Thin gold rule used to group sections inside the popup.
    static void Divider(Transform parent, string name, float y, float alpha)
    {
        var img = Img(parent, name, null);
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = new Color(1f, 0.84f, 0.45f, alpha);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(0, 0);
        rt.offsetMax = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, 2);
    }

    static TMP_InputField BuildInput(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        // Procedural name plate; matches the XP track styling.
        var bg = Frame(parent, name, 16f, 3f, Gold, NameFillTop, NameFillBottom);
        bg.raycastTarget = true;
        Place(bg.rectTransform, new Vector2(0, 1), pos, size);

        // Keep the caret clear of the rounded gold caps.
        float cap = size.y * 0.5f;
        var area = Panel(bg.transform, "TextArea");
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(cap, 10);
        area.offsetMax = new Vector2(-cap, -10);
        area.gameObject.AddComponent<RectMask2D>();

        var text = Label(area, "Text", "PLAYER");
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Left;
        text.fontSize = 40;

        var input = bg.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.characterLimit = 16;
        input.onFocusSelectAll = true;
        return input;
    }

    static void Inset(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    // AddListener registers a runtime-only callback that is NOT serialized into
    // the saved scene, so button wiring must be added as a persistent listener.
    static void OnClick(Button button, UnityEngine.Events.UnityAction action)
    {
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    // Shared popup shell: dim scrim (tap to close), ornate card, title bar,
    // close button, and an inset Content rect the caller fills.
    static (RectTransform content, Popup popup) BuildPanelShell(Transform parent, string name, string title)
    {
        var root = Panel(parent, name);
        Stretch(root);

        var scrim = Img(root, "Scrim", null);
        scrim.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrim.color = new Color(0f, 0f, 0f, 0.72f);
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);

        var cardSize = new Vector2(960, 720);
        var card = Img(root, "Card", ArtUI + "Panel_Popup2.png");
        card.type = Image.Type.Sliced;
        card.preserveAspect = false;
        card.raycastTarget = true;
        card.pixelsPerUnitMultiplier = 1086f / cardSize.y * 1.8f;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), cardSize);

        var titleText = Label(card.transform, "Title", title);
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(640, 90));
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 54;

        var close = Btn(card.transform, "CloseButton", ArtUI + "Btn_Close.png");
        Place(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -50), new Vector2(80, 80));

        // Inset content area, below the title.
        var content = Panel(card.transform, "Content");
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(58, 52);
        content.offsetMax = new Vector2(-58, -140);

        var popup = root.gameObject.AddComponent<Popup>();
        popup.root = root.gameObject;
        OnClick(close, popup.Close);
        OnClick(scrim.gameObject.AddComponent<Button>(), popup.Close);

        root.gameObject.SetActive(false);
        return (content, popup);
    }

    // Placeholder page for nav destinations not built yet.
    static Popup BuildStubPanel(Transform parent, string name, string title)
    {
        var (content, popup) = BuildPanelShell(parent, name, title);
        var body = Label(content, "Body", "Coming soon");
        Stretch(body.rectTransform);
        body.alignment = TextAlignmentOptions.Center;
        body.fontSize = 40;
        body.color = new Color(0.80f, 0.72f, 0.95f);
        return popup;
    }

    // Real Shop page: a grid of coin/gem packs. Mock purchases (see ShopPack).
    static Popup BuildShopPanel(Transform parent)
    {
        var (content, popup) = BuildPanelShell(parent, "ShopPanel", "SHOP");

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(250, 240);
        grid.spacing = new Vector2(24, 20);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        var packs = new (long coins, int gems, string price)[]
        {
            (100_000,   0, "$1.99"),
            (300_000,   0, "$4.99"),
            (700_000,   0, "$9.99"),
            (1_600_000, 0, "$19.99"),
            (0,        50, "$2.99"),
            (0,       200, "$9.99"),
        };
        foreach (var p in packs)
            BuildShopPack(content, p.coins, p.gems, p.price);

        return popup;
    }

    // Daily wheel page: circular face with reward labels, center SPIN button,
    // top pointer, status line. Logic lives in WheelPanel.
    static Popup BuildWheelPanel(Transform parent)
    {
        var (content, popup) = BuildPanelShell(parent, "WheelPanel", "DAILY WHEEL");

        var wheel = Panel(content, "Wheel");
        Place(wheel, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(460, 460));

        // Wheel face art (segmented disc + gold rim). Rotates with the reward
        // labels so the winning slot lands under the pointer.
        var face = Img(wheel, "Face", ArtUI + "Wheel_Face.png");
        face.preserveAspect = true;
        Stretch(face.rectTransform);

        var segs = new (long coins, int gems, string label)[]
        {
            (10_000,  0, "10K"),
            (0,      20, "20 GEM"),
            (25_000,  0, "25K"),
            (5_000,   0, "5K"),
            (100_000, 0, "100K"),
            (0,      50, "50 GEM"),
            (15_000,  0, "15K"),
            (50_000,  0, "50K"),
        };
        var coinRewards = new long[segs.Length];
        var gemRewards = new int[segs.Length];
        const float r = 150f;
        for (int i = 0; i < segs.Length; i++)
        {
            coinRewards[i] = segs[i].coins;
            gemRewards[i] = segs[i].gems;
            // Slices are centered at i * 45 deg (0 = top red slice).
            float ang = i * Mathf.PI * 2f / segs.Length;
            var pos = new Vector2(Mathf.Sin(ang) * r, Mathf.Cos(ang) * r);
            var seg = Label(wheel.transform, "Seg" + i, segs[i].label);
            Place(seg.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(130, 60));
            // Printed on the wheel: each label follows its own slice, so the
            // one under the pointer always reads upright.
            seg.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, -i * 360f / segs.Length);
            seg.alignment = TextAlignmentOptions.Center;
            seg.fontSize = 30;
            seg.color = new Color(1f, 0.92f, 0.6f);
        }

        var pointer = Label(content, "Pointer", "▼");
        Place(pointer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20 + 235f), new Vector2(64, 64));
        pointer.alignment = TextAlignmentOptions.Center;
        pointer.fontSize = 64;
        pointer.color = new Color(1f, 0.85f, 0.3f);

        // Center SPIN button (does not rotate with the wheel).
        var spinFrame = Frame(content, "SpinButton", 80f, 6f, GoldBright,
            new Color(0.16f, 0.42f, 0.12f, 1f), new Color(0.08f, 0.24f, 0.07f, 1f));
        spinFrame.raycastTarget = true;
        Place(spinFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(150, 150));
        var spinLbl = Label(spinFrame.transform, "Text", "SPIN");
        Stretch(spinLbl.rectTransform);
        spinLbl.alignment = TextAlignmentOptions.Center;
        spinLbl.textWrappingMode = TextWrappingModes.NoWrap;
        spinLbl.enableAutoSizing = true;
        spinLbl.fontSizeMin = 20;
        spinLbl.fontSizeMax = 42;
        var spinBtn = spinFrame.gameObject.AddComponent<Button>();
        spinBtn.targetGraphic = spinFrame;

        var status = Label(content, "Status", "");
        Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(760, 54));
        status.alignment = TextAlignmentOptions.Center;
        status.fontSize = 32;
        status.color = new Color(0.85f, 0.8f, 0.98f);

        var wp = popup.gameObject.AddComponent<WheelPanel>();
        wp.wheel = wheel;
        wp.spinButton = spinBtn;
        wp.pointer = pointer.rectTransform;
        wp.status = status;
        wp.coinRewards = coinRewards;
        wp.gemRewards = gemRewards;
        wp.sliceOffsetDeg = 0f;

        return popup;
    }

    static void BuildShopPack(RectTransform parent, long coins, int gems, string price)
    {
        bool isCoins = coins > 0;
        var packBg = Frame(parent, isCoins ? "CoinPack" : "GemPack", 20f, 3f, Gold,
            new Color(0.11f, 0.07f, 0.17f, 0.96f), new Color(0.04f, 0.02f, 0.09f, 0.96f));

        var icon = Img(packBg.transform, "Icon", ArtUI + (isCoins ? "Icon_Coin.png" : "Icon_Gem.png"));
        Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(96, 96));

        var amount = Label(packBg.transform, "Amount",
            isCoins ? coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                    : gems.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Place(amount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -122), new Vector2(232, 50));
        amount.alignment = TextAlignmentOptions.Center;
        amount.fontSize = 34;
        amount.color = new Color(1f, 0.91f, 0.62f);

        var priceBtn = Frame(packBg.transform, "Price", 16f, 3f, GoldBright,
            new Color(0.16f, 0.42f, 0.12f, 1f), new Color(0.08f, 0.24f, 0.07f, 1f));
        priceBtn.raycastTarget = true;
        Place(priceBtn.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(200, 62));
        var priceLbl = Label(priceBtn.transform, "Text", price);
        Stretch(priceLbl.rectTransform);
        priceLbl.alignment = TextAlignmentOptions.Center;
        priceLbl.fontSize = 32;
        var btn = priceBtn.gameObject.AddComponent<Button>();
        btn.targetGraphic = priceBtn;

        var pack = packBg.gameObject.AddComponent<ShopPack>();
        pack.coins = coins;
        pack.gems = gems;
        pack.button = btn;
    }

    static void SetSliceBorder(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("No importer: " + path); return; }
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool dirty = false;

        if (settings.spriteBorder != border)
        {
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            dirty = true;
        }
        // Glow art carries dark RGB under transparent pixels; without this the
        // edges fringe when filtered.
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }

        if (dirty) importer.SaveAndReimport();
    }

    const string FontDir = "Assets/Art/Fonts/";

    const string DisplayTtf = FontDir + "SairaSemiCondensed-Black.ttf";

    static TMP_FontAsset _display;
    static TMP_FontAsset DisplayFont => _display ??= EnsureFont(DisplayTtf);

    // Builds the SDF font asset on first use so the Font Asset Creator
    // window never has to be opened by hand.
    static TMP_FontAsset EnsureFont(string ttfPath)
    {
        string assetPath = System.IO.Path.ChangeExtension(ttfPath, null) + " SDF.asset";

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        // A freshly dropped .ttf may not be in the database yet on first build.
        AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceSynchronousImport);

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (ttf == null)
        {
            Debug.LogError("Font missing, falling back to default: " + ttfPath);
            return null;
        }

        var font = TMP_FontAsset.CreateFontAsset(ttf);
        AssetDatabase.CreateAsset(font, assetPath);

        // Atlas and material are loose objects; nest them or the asset breaks
        // on reload.
        foreach (var atlas in font.atlasTextures)
        {
            atlas.name = "Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }
        font.material.name = "Material";
        AssetDatabase.AddObjectToAsset(font.material, font);

        AssetDatabase.SaveAssets();
        Debug.Log("Created font asset: " + assetPath);
        return font;
    }

    static Sprite Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // Belt and braces: LoadAssetAtPath<Sprite> returns whichever sub-sprite
        // comes first, so a texture that slipped back into Multiple mode would
        // silently swap the art for a few-pixel fragment. Take the largest.
        Sprite best = null;
        float bestArea = -1f;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is not Sprite s) continue;
            float area = s.rect.width * s.rect.height;
            if (area > bestArea) { bestArea = area; best = s; }
        }

        if (best == null) Debug.LogError("Missing sprite, UI will render blank: " + path);
        return best;
    }

    static Image Img(Transform parent, string name, string path)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = Load(path);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    const string FrameMatPath = "Assets/Art/Materials/FrameSDF.mat";
    static Material _frameMat;
    static Material FrameMaterial =>
        _frameMat != null ? _frameMat : (_frameMat = EnsureFrameMaterial());

    // A real material asset beats a runtime Shader.Find: it serialises into the
    // scene, survives domain reloads, and can't be stripped from a build. If
    // this is missing every ThemedFrame in the game breaks at once.
    static Material EnsureFrameMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FrameMatPath);
        if (mat != null) return mat;

        var shader = Shader.Find(ThemedFrame.ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{ThemedFrame.ShaderName}' not found — themed frames " +
                           "(pills, XP bar, shop packs, SPIN) will render as flat quads.");
            return null;
        }

        System.IO.Directory.CreateDirectory("Assets/Art/Materials");
        mat = new Material(shader) { name = "FrameSDF" };
        AssetDatabase.CreateAsset(mat, FrameMatPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created material: " + FrameMatPath);
        return mat;
    }

    // Procedural rounded-rect frame (no sprite) — see ThemedFrame.cs.
    static ThemedFrame Frame(Transform parent, string name, float radius, float border,
        Color borderColor, Color fillTop, Color fillBottom)
    {
        var go = new GameObject(name, typeof(ThemedFrame));
        go.transform.SetParent(parent, false);
        var f = go.GetComponent<ThemedFrame>();
        if (FrameMaterial != null) f.material = FrameMaterial;
        f.cornerRadius = radius;
        f.borderThickness = border;
        f.borderColor = borderColor;
        f.fillTop = fillTop;
        f.fillBottom = fillBottom;
        f.raycastTarget = false;
        return f;
    }

    static Button Btn(Transform parent, string name, string path)
    {
        var img = Img(parent, name, path);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;   // without this a tap gives no visual feedback
        return btn;
    }

    // Invisible placeholder button: transparent hit area, no art, no label.
    static Button HitButton(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var hit = go.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);   // transparent, still raycastable
        hit.raycastTarget = true;
        return go.AddComponent<Button>();
    }

    static RectTransform Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static TMP_Text Label(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (DisplayFont != null) t.font = DisplayFont;
        t.text = text;
        t.fontSize = 48;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
