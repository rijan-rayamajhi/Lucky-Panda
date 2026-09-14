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
        // Measured from the art: borders enclose the ornate end caps so only
        // the flat centre stretches. Vector4 is (left, bottom, right, top).
        SetSliceBorder(ArtUI + "Btn_Gold.png", new Vector4(218, 99, 218, 94));
        SetSliceBorder(ArtUI + "Row_Plate.png", new Vector4(213, 43, 215, 43));

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
        var audioGo = new GameObject("AudioManager", typeof(AudioManager));
        var am = audioGo.GetComponent<AudioManager>();
        am.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Lobby_Music.mp3");
        am.clickSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/mixkit-select-click-1109.wav");

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
        Place(piggy.rectTransform, new Vector2(1, 0.5f), new Vector2(-168, 0), new Vector2(88, 88));

        var settingsBtn = Btn(hud, "SettingsButton", ArtUI + "Icon_Settings.png");
        Place(settingsBtn.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(86, 86));

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
        NavTab[] navTabs   = { NavTab.Cards, NavTab.Puzzle, NavTab.Quests, NavTab.Wheel, NavTab.Inbox, NavTab.Club };
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
            BuildNavBadge(i.rectTransform, navTabs[k]);
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

        var bubbleSize = new Vector2(640, 440);
        var bubble = Img(msgRoot, "Bubble", ArtUI + "Bubble_Speech.png");
        bubble.type = Image.Type.Sliced;
        bubble.preserveAspect = false;
        // Same trick as the pill: keep the painted corners and the tail intact
        // while the middle stretches.
        bubble.pixelsPerUnitMultiplier = BubbleSourceHeight / bubbleSize.y;
        Place(bubble.rectTransform, new Vector2(0, 0), new Vector2(390, 270), bubbleSize);

        var bubbleText = Label(bubble.transform, "Text", "WELCOME BACK!");
        bubbleText.color = new Color(0.45f, 0.09f, 0.06f);
        bubbleText.fontSize = 42;
        bubbleText.enableAutoSizing = true;
        bubbleText.fontSizeMin = 22;
        bubbleText.fontSizeMax = 46;
        bubbleText.lineSpacing = -8f;
        var btRT = bubbleText.rectTransform;
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.offsetMin = new Vector2(40, 36);
        btRT.offsetMax = new Vector2(-64, -48);

        // Close button to dismiss the host message.
        var bubbleClose = Btn(bubble.transform, "BubbleClose", ArtUI + "Btn_Close.png");
        Place(bubbleClose.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-36, -36), new Vector2(64, 64));

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
            Popup page;
            switch (navTitles[k])
            {
                case "WHEEL":  page = BuildWheelPanel(canvasGO.transform); break;
                case "QUESTS": page = BuildQuestsPanel(canvasGO.transform); break;
                case "INBOX":  page = BuildInboxPanel(canvasGO.transform); break;
                case "CLUB":   page = BuildClubPanel(canvasGO.transform); break;
                case "PUZZLE": page = BuildPuzzlePanel(canvasGO.transform); break;
                case "CARDS":  page = BuildCardsPanel(canvasGO.transform); break;
                default:       page = BuildStubPanel(canvasGO.transform, navTitles[k] + "Panel", navTitles[k]); break;
            }
            OnClick(navButtons[k], page.Open);
        }

        // Shop page opened by the BUY button.
        var shop = BuildShopPanel(canvasGO.transform);

        // Settings page opened by the Settings button.
        var settingsPanel = BuildSettingsPanel(canvasGO.transform);
        OnClick(settingsBtn, settingsPanel.Open);

        // Shared reward celebration, and the asset handles runtime code needs.
        BuildRewardPopup(canvasGO.transform);
        BuildContentRefs(canvasGO);

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

        // Ensure all SafeArea rects are saved with canonical full-stretch anchors (0,0)-(1,1)
        // so that in-editor serialization never bakes corrupted coordinates.
        foreach (var sa in Object.FindObjectsByType<SafeArea>(FindObjectsSortMode.None))
        {
            var srt = sa.GetComponent<RectTransform>();
            if (srt != null)
            {
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
            }
        }

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
        content.offsetMax = new Vector2(-58, -150);

        // Reuse the ornate jackpot banner as the title plate sitting inside the card top.
        var title = Img(card.transform, "TitleBar", ArtUI + "Bar_Major.png");
        title.preserveAspect = true;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(460, 130));
        var titleText = Label(title.transform, "Text", "PROFILE");
        Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(380, 72));
        titleText.fontSize = 44;

        // Avatar
        var avatar = Img(content, "Avatar", ArtUI + "Icon_Profile.png");
        Place(avatar.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(148, 148));

        // Name input
        var nameField = BuildInput(content, "NameInput", new Vector2(174, 0), new Vector2(460, 68));

        // Level text
        var levelText = Label(content, "LevelText", "LVL 1");
        Place(levelText.rectTransform, new Vector2(0, 1), new Vector2(656, -14), new Vector2(200, 48));
        levelText.alignment = TextAlignmentOptions.MidlineLeft;
        levelText.fontSize = 32;
        levelText.color = GoldBright;

        // XP progress bar
        var xpSize = new Vector2(716, 44);
        var xpBg = Frame(content, "XpBg", xpSize.y * 0.5f, 6f, Gold, TrackFillTop, TrackFillBottom);
        Place(xpBg.rectTransform, new Vector2(0, 1), new Vector2(174, -92), xpSize);

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
        Place(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(72, 72));

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
        return BuildPanelShell<Popup>(parent, name, title, new Vector2(960, 720));
    }

    // T is the panel component itself, so a page carries its own behaviour
    // instead of a bare Popup plus a sibling script.
    static (RectTransform content, T popup) BuildPanelShell<T>(Transform parent, string name,
        string title, Vector2 cardSize) where T : Popup
    {
        var root = Panel(parent, name);
        Stretch(root);

        var scrim = Img(root, "Scrim", null);
        scrim.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrim.color = new Color(0f, 0f, 0f, 0.72f);
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);

        // Clamped inside safe area so device notches and cutouts don't clip the card
        var safeContainer = Panel(root, "SafeContainer");
        Stretch(safeContainer);
        safeContainer.gameObject.AddComponent<SafeArea>();

        // Constrain card width to max 1280 to ensure wide aspect/notch devices never clip edges
        Vector2 clampedSize = new Vector2(Mathf.Min(cardSize.x, 1280f), cardSize.y);

        var card = Img(safeContainer, "Card", ArtUI + "Panel_Popup2.png");
        card.type = Image.Type.Sliced;
        card.preserveAspect = false;
        card.raycastTarget = true;
        card.pixelsPerUnitMultiplier = 1086f / clampedSize.y * 1.8f;

        // A tall card pushed the banner up into the top HUD, covering the coin
        // pill. Drop it just far enough to clear, and leave the short cards
        // (wheel, shop, profile) exactly where they were.
        const float CanvasHalfHeight = 540f;
        const float BannerHeadroom = 150f;
        float cardY = Mathf.Min(-40f, CanvasHalfHeight - BannerHeadroom - clampedSize.y * 0.5f);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, cardY), clampedSize);

        // Ornate banner sitting cleanly inside the top of the asset card frame,
        // without overflowing or sticking out over the top gold border.
        var banner = Img(card.transform, "TitleBar", ArtUI + "Bar_Major.png");
        banner.preserveAspect = true;
        Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(460, 130));

        var titleText = Label(banner.transform, "Text", title);
        Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(380, 72));
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 44;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 22;
        titleText.fontSizeMax = 46;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        UIFactory.Shadowed(titleText, 0.85f, 3f);

        var close = Btn(card.transform, "CloseButton", ArtUI + "Btn_Close.png");
        Place(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(72, 72));

        // Content sits inside the asset frame's natural purple interior.
        var content = Panel(card.transform, "Content");
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(58, 48);
        content.offsetMax = new Vector2(-58, -150);

        var popup = root.gameObject.AddComponent<T>();
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

    static SettingsPanel BuildSettingsPanel(Transform parent)
    {
        var (content, panel) = BuildPanelShell<SettingsPanel>(parent, "SettingsPanel", "SETTINGS", new Vector2(880, 640));

        float startY = -24f;
        float rowStep = 96f;

        panel.musicToggle = BuildSettingRow(content, "MusicRow", "BACKGROUND MUSIC", startY, out panel.musicStatusText);
        panel.sfxToggle = BuildSettingRow(content, "SfxRow", "SOUND EFFECTS", startY - rowStep, out panel.sfxStatusText);
        panel.hapticsToggle = BuildSettingRow(content, "HapticsRow", "VIBRATION & HAPTICS", startY - rowStep * 2, out panel.hapticsStatusText);

        Divider(content, "SettingsRule", startY - rowStep * 2.8f, 0.25f);

        var version = Label(content, "VersionText", "LUCKY PANDA CASINO  •  v1.0.0");
        Place(version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(600, 36));
        version.alignment = TextAlignmentOptions.Center;
        version.fontSize = 20;
        version.color = UIFactory.Dim;

        return panel;
    }

    static Button BuildSettingRow(RectTransform parent, string name, string labelText, float yPos, out TMP_Text statusLabel)
    {
        var row = Panel(parent, name);
        Place(row, new Vector2(0f, 1f), new Vector2(40, yPos), new Vector2(680, 72));
        row.pivot = new Vector2(0, 1);

        var title = Label(row, "Label", labelText);
        Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(460, 50));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.fontSize = 28;
        title.color = UIFactory.Cream;
        UIFactory.Shadowed(title, 0.7f, 2f);

        var toggleBtn = PillButton(row, "ToggleBtn", "ON", new Vector2(160, 56));
        Place(toggleBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-10, 0), new Vector2(160, 56));
        statusLabel = toggleBtn.GetComponentInChildren<TMP_Text>();

        return toggleBtn;
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

        // Drawn as a mesh, not the character U+25BC: neither shipped font has
        // that glyph and TMP has no fallback, so it was rendering as a box.
        var pointer = ShapeOf(content, "Pointer", UIShape.TriangleDown, new Color(1f, 0.85f, 0.3f));
        Place(pointer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20 + 235f), new Vector2(56, 66));

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

    // ---- bottom-nav destinations --------------------------------------

    static QuestsPanel BuildQuestsPanel(Transform parent)
    {
        var (content, panel) = BuildPanelShell<QuestsPanel>(parent, "QuestsPanel", "QUESTS", new Vector2(1400, 880));

        var subtitle = Label(content, "Subtitle", "RESETS DAILY");
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(700, 36));
        subtitle.fontSize = 26;
        subtitle.color = GoldBright;

        var host = Panel(content, "ScrollHost");
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = Vector2.zero;
        host.offsetMax = new Vector2(0, -48);

        panel.listContent = UIFactory.Scroll(host, "Scroll", 12f);
        panel.subtitle = subtitle;
        return panel;
    }

    static InboxPanel BuildInboxPanel(Transform parent)
    {
        var (content, panel) = BuildPanelShell<InboxPanel>(parent, "InboxPanel", "INBOX", new Vector2(1400, 880));

        var host = Panel(content, "ScrollHost");
        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.offsetMin = new Vector2(0, 108);
        host.offsetMax = Vector2.zero;

        panel.listContent = UIFactory.Scroll(host, "Scroll", 12f);

        var empty = Label(content, "Empty", "NO MESSAGES YET");
        Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 60));
        empty.fontSize = 34;
        empty.color = new Color(0.80f, 0.72f, 0.95f);
        panel.emptyLabel = empty;

        var claimAll = PillButton(content, "ClaimAll", "CLAIM ALL", new Vector2(300, 68));
        Place(claimAll.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(300, 68));
        panel.claimAllButton = claimAll;

        return panel;
    }

    static ClubPanel BuildClubPanel(Transform parent)
    {
        var (content, panel) = BuildPanelShell<ClubPanel>(parent, "ClubPanel", "PANDA CLUB", new Vector2(1100, 820));

        var crest = Img(content, "Crest", ArtUI + "Bar_Grand.png");
        crest.preserveAspect = true;
        Place(crest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(420, 150));
        panel.crest = crest;

        var tierName = Label(crest.transform, "TierName", "BRONZE");
        Place(tierName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(340, 70));
        tierName.fontSize = 46;
        panel.tierNameText = tierName;

        var stars = Panel(content, "StarRow");
        Place(stars, new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(300, 44));
        UIFactory.Configure(stars.gameObject.AddComponent<HorizontalLayoutGroup>(), 12f, TextAnchor.MiddleCenter);
        panel.starRow = stars;

        var bar = Frame(content, "PointsTrack", 18f, 4f, Gold, TrackFillTop, TrackFillBottom);
        Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -228), new Vector2(720, 38));
        var fill = Frame(bar.transform, "Fill", 18f, 0f, XpFillTop, XpFillTop, XpFillBottom);
        Inset(fill.rectTransform, 5);
        panel.progressFill = fill;

        var points = Label(bar.transform, "Points", "0 / 2,500");
        Stretch(points.rectTransform);
        points.fontSize = 24;
        panel.pointsText = points;

        var next = Label(content, "NextTier", "2,500 POINTS TO SILVER");
        Place(next.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -274), new Vector2(800, 34));
        next.fontSize = 26;
        next.color = new Color(0.80f, 0.72f, 0.95f);
        panel.nextTierLabel = next;

        Divider(content, "ClubRule", -316, 0.45f);

        panel.perksNowColumn = PerkColumn(content, "PerksNow", "ACTIVE PERKS", -336, -1);
        panel.perksNextColumn = PerkColumn(content, "PerksNext", "NEXT TIER", -336, 1);

        return panel;
    }

    static RectTransform PerkColumn(RectTransform content, string name, string heading, float y, int side)
    {
        float x = side < 0 ? 0f : 500f;

        var plate = Panel(content, name + "Plate");
        Place(plate, new Vector2(0, 1), new Vector2(x, y + 8), new Vector2(484, 248));
        plate.pivot = new Vector2(0, 1);

        var header = Label(plate.transform, "Header", heading);
        Place(header.rectTransform, new Vector2(0, 1), new Vector2(24, -14), new Vector2(440, 38));
        header.alignment = TextAlignmentOptions.MidlineLeft;
        header.fontSize = 26;
        header.color = side < 0 ? GoldBright : new Color(0.80f, 0.72f, 0.95f);
        UIFactory.Shadowed(header);

        var column = Panel(plate.transform, name);
        Place(column, new Vector2(0, 1), new Vector2(24, -60), new Vector2(436, 180));
        column.pivot = new Vector2(0, 1);
        var layout = UIFactory.Configure(column.gameObject.AddComponent<VerticalLayoutGroup>(), 6f, TextAnchor.UpperLeft);
        layout.childForceExpandWidth = true;
        return column;
    }

    static PuzzlePanel BuildPuzzlePanel(Transform parent)
    {
        var (content, panel) = BuildPanelShell<PuzzlePanel>(parent, "PuzzlePanel", "PUZZLE", new Vector2(1000, 880));

        var title = Label(content, "PuzzleTitle", "PANDA PARADISE");
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(760, 46));
        title.fontSize = 36;
        title.enableAutoSizing = true;
        title.fontSizeMin = 20;
        title.fontSizeMax = 38;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.color = GoldBright;
        UIFactory.Shadowed(title, 0.8f, 3f);
        panel.titleText = title;

        var progress = Label(content, "Progress", "0 / 9");
        Place(progress.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(400, 42));
        progress.fontSize = 32;
        progress.color = new Color(1f, 0.91f, 0.62f);
        panel.progressText = progress;



        const float board = 470f;
        var boardRect = Panel(content, "Board");
        Place(boardRect, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(board, board));
        boardRect.pivot = new Vector2(0.5f, 1f);
        var grid = boardRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2((board - 12f) / 3f, (board - 12f) / 3f);
        grid.spacing = new Vector2(6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        panel.grid = boardRect;
        panel.boardSize = board;

        // Sits clear of the board's bottom edge: the board ends 156px up from
        // the content floor, and these two stack below that.
        var hint = Label(content, "Hint", "Pieces come from quests, card sets and the wheel");
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(860, 44));
        hint.fontSize = 24;
        hint.color = new Color(0.80f, 0.72f, 0.95f);
        panel.hintText = hint;

        var claim = PillButton(content, "ClaimPuzzle", "KEEP COLLECTING", new Vector2(380, 76));
        Place(claim.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(380, 76));
        panel.claimButton = claim;

        return panel;
    }

    static CardsPanel BuildCardsPanel(Transform parent)
    {
        var (content, panel) = BuildPanelShell<CardsPanel>(parent, "CardsPanel", "CARD ALBUM", new Vector2(1400, 900));

        var tabs = Panel(content, "Tabs");
        Place(tabs, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(1160, 56));
        UIFactory.Configure(tabs.gameObject.AddComponent<HorizontalLayoutGroup>(), 10f, TextAnchor.MiddleCenter);
        panel.tabsRow = tabs;

        var album = Panel(content, "Album");
        Place(album, new Vector2(0, 1), new Vector2(0, -68), new Vector2(440, 544));
        album.pivot = new Vector2(0, 1);
        var grid = album.gameObject.AddComponent<GridLayoutGroup>();
        // 138x174 maintains the card frame art's 0.79 aspect ratio while fitting 3 rows inside the card bounds.
        grid.cellSize = new Vector2(138, 174);
        grid.spacing = new Vector2(12, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;
        panel.grid = album;

        // Right-hand set summary sits directly on the clean purple card canvas
        var side = Panel(content, "SetPlate");
        Place(side, new Vector2(0, 1), new Vector2(456, -68), new Vector2(708, 544));
        side.pivot = new Vector2(0, 1);

        var setName = Label(side.transform, "SetName", "BAMBOO GROVE");
        Place(setName.rectTransform, new Vector2(0, 1), new Vector2(24, -14), new Vector2(650, 48));
        setName.alignment = TextAlignmentOptions.MidlineLeft;
        setName.fontSize = 36;
        setName.enableAutoSizing = true;
        setName.fontSizeMin = 22;
        setName.fontSizeMax = 38;
        setName.textWrappingMode = TextWrappingModes.NoWrap;
        setName.color = GoldBright;
        UIFactory.Shadowed(setName, 0.8f, 3f);
        panel.setNameText = setName;

        var count = Label(side.transform, "SetCount", "0 / 9");
        Place(count.rectTransform, new Vector2(0, 1), new Vector2(24, -68), new Vector2(300, 38));
        count.alignment = TextAlignmentOptions.MidlineLeft;
        count.fontSize = 30;
        count.color = new Color(1f, 0.91f, 0.62f);
        panel.countText = count;

        var track = Frame(side.transform, "SetTrack", 16f, 3f, Gold, TrackFillTop, TrackFillBottom);
        Place(track.rectTransform, new Vector2(0, 1), new Vector2(24, -114), new Vector2(650, 30));
        var setFill = Frame(track.transform, "Fill", 16f, 0f, XpFillTop, XpFillTop, XpFillBottom);
        Inset(setFill.rectTransform, 4);
        panel.setProgressFill = setFill;

        Divider(side.transform as RectTransform, "SetRule", -156, 0.35f);

        var rewardHeader = Label(side.transform, "RewardHeader", "SET REWARD");
        Place(rewardHeader.rectTransform, new Vector2(0, 1), new Vector2(24, -172), new Vector2(400, 30));
        rewardHeader.alignment = TextAlignmentOptions.MidlineLeft;
        rewardHeader.fontSize = 22;
        rewardHeader.color = new Color(0.80f, 0.72f, 0.95f);

        var rewardRow = Panel(side.transform, "SetReward");
        Place(rewardRow, new Vector2(0, 1), new Vector2(24, -208), new Vector2(650, 44));
        rewardRow.pivot = new Vector2(0, 1);
        panel.setRewardRow = rewardRow;

        var claimSet = PillButton(side.transform, "ClaimSet", "INCOMPLETE", new Vector2(290, 68));
        Place(claimSet.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(24, -452), new Vector2(290, 68));
        panel.claimSetButton = claimSet;

        var openPack = PillButton(side.transform, "OpenPack", "NO PACKS", new Vector2(290, 68));
        Place(openPack.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(336, -452), new Vector2(290, 68));
        panel.openPackButton = openPack;

        BuildPackOverlay(panel);
        return panel;
    }

    static void BuildPackOverlay(CardsPanel panel)
    {
        var overlay = Panel(panel.transform, "PackOverlay");
        Stretch(overlay);

        var scrim = Img(overlay, "Scrim", null);
        scrim.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrim.color = new Color(0.02f, 0.01f, 0.05f, 0.93f);
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);

        var headline = Label(overlay, "Headline", "CARD PACK");
        Place(headline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1100, 70));
        headline.fontSize = 52;
        headline.color = GoldBright;
        panel.packHeadline = headline;

        var row = Panel(overlay, "Cards");
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 300));
        UIFactory.Configure(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 30f, TextAnchor.MiddleCenter);
        panel.packCardRow = row;

        var tap = Label(overlay, "Tap", "TAP TO CONTINUE");
        Place(tap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(700, 44));
        tap.fontSize = 28;
        tap.color = new Color(0.80f, 0.72f, 0.95f);

        var dismiss = scrim.gameObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        panel.packDismissButton = dismiss;

        overlay.gameObject.SetActive(false);
        panel.packOverlay = overlay.gameObject;
    }

    // ---- shared additions ----------------------------------------------

    static void BuildNavBadge(RectTransform icon, NavTab tab)
    {
        var badge = Panel(icon, "Badge");
        Place(badge, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(46f, 46f));

        var disc = ShapeOf(badge, "Disc", UIShape.Circle, new Color(0.86f, 0.17f, 0.20f, 1f));
        Stretch(disc.rectTransform);

        var count = Label(badge.transform, "Count", "0");
        Stretch(count.rectTransform);
        count.fontSize = 26;
        count.alignment = TextAlignmentOptions.Center;

        var nb = icon.gameObject.AddComponent<NavBadge>();
        nb.tab = tab;
        nb.badgeRoot = badge.gameObject;
        nb.countText = count;

        badge.gameObject.SetActive(false);
    }

    static void BuildRewardPopup(Transform canvas)
    {
        var root = Panel(canvas, "RewardPopup");
        Stretch(root);

        var scrim = Img(root, "Scrim", null);
        scrim.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrim.color = new Color(0f, 0f, 0f, 0.55f);
        scrim.raycastTarget = true;
        Stretch(scrim.rectTransform);

        var card = Frame(root, "Card", 30f, 6f, GoldBright, PillFillTop, PillFillBottom);
        card.raycastTarget = true;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(820, 300));

        var headline = Label(card.transform, "Headline", "YOU GOT");
        Place(headline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(740, 66));
        headline.fontSize = 50;
        headline.color = GoldBright;
        headline.enableAutoSizing = true;
        headline.fontSizeMin = 26;
        headline.fontSizeMax = 52;

        var body = Label(card.transform, "Body", "");
        Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(740, 150));
        body.fontSize = 38;
        body.enableAutoSizing = true;
        body.fontSizeMin = 20;
        body.fontSizeMax = 40;

        var popup = canvas.gameObject.AddComponent<RewardPopup>();
        popup.root = root.gameObject;
        popup.card = card.rectTransform;
        popup.headline = headline;
        popup.body = body;

        OnClick(scrim.gameObject.AddComponent<Button>(), popup.Dismiss);
        root.gameObject.SetActive(false);
    }

    const string ArtSym = "Assets/Art/Symbols/";
    const string ArtPuzzle = "Assets/Art/Puzzles/";

    static void BuildContentRefs(GameObject canvasGO)
    {
        var refs = canvasGO.AddComponent<ContentRefs>();
        refs.iconCoin = Load(ArtUI + "Icon_Coin.png");
        refs.iconGem = Load(ArtUI + "Icon_Gem.png");
        refs.iconGift = Load(ArtUI + "Icon_Gift.png");
        refs.panelCard = Load(ArtUI + "Panel_Popup2.png");
        refs.closeButton = Load(ArtUI + "Btn_Close.png");
        refs.barMajor = Load(ArtUI + "Bar_Major.png");
        refs.barGrand = Load(ArtUI + "Bar_Grand.png");
        refs.buttonPlate = Load(ArtUI + "Btn_Gold.png");
        refs.rowPlate = Load(ArtUI + "Row_Plate.png");
        refs.cardFrame = Load(ArtUI + "Card_Frame.png");

        refs.cardSymbols = new[]
        {
            Load(ArtSym + "Sym_10.png"), Load(ArtSym + "Sym_J.png"), Load(ArtSym + "Sym_Q.png"),
            Load(ArtSym + "Sym_K.png"), Load(ArtSym + "Sym_A.png"), Load(ArtSym + "Sym_Wild.png"),
            Load(ArtSym + "Sym_Scatter.png")
        };

        // Index-aligned to PuzzleDef.imageIndex in Content.Puzzles. Each source
        // is 1248px square, which divides evenly by both 3 and 4 so neither
        // board shape slices on a half pixel.
        refs.puzzleImages = new[]
        {
            Load(ArtPuzzle + "Puzzle_01.png"),   // PANDA PARADISE, 3x3
            Load(ArtPuzzle + "Puzzle_02.png"),   // GOLDEN PAGODA,  3x3
            Load(ArtPuzzle + "Puzzle_03.png")    // JADE FORTUNE,   4x4
        };

        refs.frameMaterial = FrameMaterial;
        refs.displayFont = DisplayFont;
    }

    static ShapeGraphic ShapeOf(Transform parent, string name, UIShape shape, Color color)
    {
        var go = new GameObject(name, typeof(ShapeGraphic));
        go.transform.SetParent(parent, false);
        var s = go.GetComponent<ShapeGraphic>();
        s.shape = shape;
        s.color = color;
        s.raycastTarget = false;
        return s;
    }

    static Button PillButton(Transform parent, string name, string text, Vector2 size)
    {
        Graphic graphic;
        var plate = Load(ArtUI + "Btn_Gold.png");
        if (plate != null)
        {
            var img = Img(parent, name, ArtUI + "Btn_Gold.png");
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.raycastTarget = true;
            img.pixelsPerUnitMultiplier = UIFactory.ButtonArtHeight / Mathf.Max(1f, size.y);
            img.rectTransform.sizeDelta = size;
            graphic = img;
        }
        else
        {
            var frame = Frame(parent, name, size.y * 0.5f, 3f, GoldBright,
                new Color(0.16f, 0.42f, 0.12f, 1f), new Color(0.08f, 0.24f, 0.07f, 1f));
            frame.raycastTarget = true;
            frame.rectTransform.sizeDelta = size;
            graphic = frame;
        }

        var label = Label(graphic.transform, "Text", text);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(size.x * 0.14f, 0f);
        label.rectTransform.offsetMax = new Vector2(-size.x * 0.14f, 0f);
        label.fontSize = 30;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = 32;
        UIFactory.Shadowed(label, 0.85f, 2f);

        var btn = graphic.gameObject.AddComponent<Button>();
        btn.targetGraphic = graphic;
        return btn;
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
