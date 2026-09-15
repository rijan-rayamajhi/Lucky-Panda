using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class SlotGameBuilder
{
    const string ArtUI = "Assets/Art/UI/";
    const string ArtBG = "Assets/Art/Backgrounds/";
    const string ArtSymbols = "Assets/Art/Symbols/";

    static readonly Color GoldBright    = new Color(1f, 0.88f, 0.45f);
    static readonly Color GemBlue       = new Color(0.28f, 0.62f, 0.98f);
    static readonly Color PillFillTop   = new Color(0.16f, 0.08f, 0.28f, 0.94f);
    static readonly Color PillFillBottom= new Color(0.06f, 0.02f, 0.12f, 0.96f);
    static readonly Color PillTop       = new Color(0.12f, 0.09f, 0.16f, 0.96f);
    static readonly Color PillBottom    = new Color(0.05f, 0.03f, 0.08f, 0.96f);

    [MenuItem("Lucky Panda/Open Slot Game Scene")]
    public static void OpenSlotGameScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SlotGame.unity");
    }

    [MenuItem("Lucky Panda/Open Lobby Scene")]
    public static void OpenLobbyScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
    }

    [MenuItem("Lucky Panda/Build Slot Game Scene")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        SpriteImportOptimizer.EnsureSingleSpriteMode();

        // A stale atlas hides any sprite added to Assets/Art/UI since the atlas
        // was last packed — it renders as nothing at runtime even though the
        // loose import is fine, since the Editor's Sprite Packer is always-on.
        SpriteImportOptimizer.BuildAtlases();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 1. Canvas setup
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
        var audioGO = new GameObject("AudioManager", typeof(AudioManager));
        var am = audioGO.GetComponent<AudioManager>();
        am.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Lobby_Music.mp3");
        am.clickSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/mixkit-select-click-1109.wav");
        am.coinSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/mixkit-payout-award-ding-1935.wav");

        // 2. Background
        var bg = Img(canvasGO.transform, "Background", ArtBG + "Bg_Slot777.png");
        bg.preserveAspect = false;
        Stretch(bg.rectTransform);

        // 3. SafeArea
        var safe = Panel(canvasGO.transform, "SafeArea");
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();

        // 4. Top HUD (120px height, exactly matching LobbyBuilder)
        var hud = Panel(safe, "TopHUD");
        Anchor(hud, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
        hud.sizeDelta = new Vector2(0, 120);
        hud.anchoredPosition = Vector2.zero;

        // Back to Lobby button (Replaces the avatar badge at left: anchor (0, 0.5f), size (130, 72))
        var backBtn = BtnPill(hud, "BackToLobbyBtn", "< LOBBY", new Vector2(0, 0.5f), new Vector2(75, 0), new Vector2(130, 72));

        // Exact identical Currency Pills from LobbyBuilder.cs (size 240x84 and 380x84)
        var gemText = CurrencyPill(hud, "GemPill", ArtUI + "Icon_Gem.png", "150",
            new Vector2(230, 0), new Vector2(240, 84), GemBlue);
        var coinText = CurrencyPill(hud, "CoinPill", ArtUI + "Icon_Coin.png", "1,000,000",
            new Vector2(510, 0), new Vector2(380, 84), GoldBright);

        // 5. Jackpot Tickers (Row below HUD - 920px container matching the slot cabinet width)
        var jackpots = Panel(safe, "JackpotHeader");
        Place(jackpots, new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(920, 62));
        var jpLayout = jackpots.gameObject.AddComponent<HorizontalLayoutGroup>();
        jpLayout.childAlignment = TextAnchor.MiddleCenter;
        jpLayout.spacing = 8;
        jpLayout.childForceExpandWidth = false;
        jpLayout.childForceExpandHeight = false;

        var grandText = JackpotPlate(jackpots, "GRAND", ArtUI + "Bar_Grand.png", "1,250,000");
        var majorText = JackpotPlate(jackpots, "MAJOR", ArtUI + "Bar_Major.png", "285,000");
        var minorText = JackpotPlate(jackpots, "MINOR", ArtUI + "Bar_Minor.png", "65,000");
        var miniText  = JackpotPlate(jackpots, "MINI",  ArtUI + "Bar_Mini.png",  "14,500");

        // 6. Slot Cabinet & Reel Matrix (Center)
        // Frame_Classic777 is 1448x1086 (1.333:1). Sized to 920x690 on canvas.
        var cabinet = Panel(safe, "SlotCabinet");
        Place(cabinet, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(920, 690));

        // Inner velvet backing behind reels (sized to inner window)
        // Inner window is 74.0% of width (680px), 46.1% of height (318px)
        var backdrop = Frame(cabinet, "VelvetBackdrop", 16f, 0f, Color.clear,
            new Color(0.08f, 0.03f, 0.12f, 0.98f), new Color(0.03f, 0.01f, 0.06f, 0.98f));
        Place(backdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(676, 314));

        // Reel window with mask
        var reelWindow = Panel(cabinet, "ReelWindow");
        Place(reelWindow, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(670, 310));
        var mask = reelWindow.gameObject.AddComponent<RectMask2D>();

        // Create 3 Reel Columns
        float colWidth = 214f;
        float spacing = 12f;
        float startX = -colWidth - spacing;

        var reels = new ReelColumn[SlotDef.Cols];
        for (int c = 0; c < SlotDef.Cols; c++)
        {
            var colGO = new GameObject($"Reel_{c}", typeof(RectTransform), typeof(ReelColumn));
            colGO.transform.SetParent(reelWindow, false);
            var colRT = colGO.GetComponent<RectTransform>();
            Place(colRT, new Vector2(0.5f, 0.5f), new Vector2(startX + c * (colWidth + spacing), 0), new Vector2(colWidth, 310));

            var rc = colGO.GetComponent<ReelColumn>();
            rc.container = colRT;
            rc.symbolImages = new Image[SlotDef.Rows];

            // 3 rows inside column
            float rowHeight = 102f;
            float rowStartY = rowHeight;
            for (int r = 0; r < SlotDef.Rows; r++)
            {
                var symImg = Img(colRT, $"Sym_{r}", ArtSymbols + "Sym_10.png");
                symImg.preserveAspect = true;
                Place(symImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, rowStartY - r * rowHeight), new Vector2(190, 100));
                rc.symbolImages[r] = symImg;
            }
            reels[c] = rc;
        }

        // Two golden vertical dividers between reels
        for (int d = 0; d < 2; d++)
        {
            var divider = Img(cabinet, $"Divider_{d}", null);
            divider.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            divider.color = new Color(1f, 0.82f, 0.35f, 0.45f);
            float divX = -colWidth / 2f - spacing / 2f + d * (colWidth + spacing);
            Place(divider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(divX, -16), new Vector2(3, 300));
        }

        // Ornate 3D 777 Imperial Frame overlay sitting on top of the reels
        var frameImg = Img(cabinet, "FrameOverlay", ArtUI + "Frame_Classic777.png");
        frameImg.preserveAspect = true;
        Stretch(frameImg.rectTransform);

        // 7. Bottom Control Deck
        var deck = Panel(safe, "BottomDeck");
        Anchor(deck, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
        deck.sizeDelta = new Vector2(0, 140);
        deck.anchoredPosition = Vector2.zero;

        // Win display (Sleek 240px pill with auto-sizing)
        var winPill = Frame(deck, "WinPill", 34f, 5f, GoldBright, PillTop, PillBottom);
        Place(winPill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-440, 0), new Vector2(240, 78));
        var winTitle = Label(winPill.transform, "Title", "WIN");
        winTitle.fontSize = 16;
        winTitle.color = new Color(1f, 0.85f, 0.4f, 0.85f);
        Place(winTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 17), new Vector2(220, 22));
        var winAmountText = Label(winPill.transform, "Amount", "0");
        winAmountText.fontSize = 28;
        winAmountText.enableAutoSizing = true;
        winAmountText.fontSizeMin = 14;
        winAmountText.fontSizeMax = 28;
        winAmountText.textWrappingMode = TextWrappingModes.NoWrap;
        winAmountText.color = GoldBright;
        Place(winAmountText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(220, 36));

        // Bet minus button
        var betMinus = BtnText(deck, "BetMinus", "-", new Vector2(0.5f, 0.5f), new Vector2(-285, 0), new Vector2(50, 50));

        // Bet display (200px wide)
        var betPill = Frame(deck, "BetPill", 34f, 5f, GoldBright, PillTop, PillBottom);
        Place(betPill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150, 0), new Vector2(200, 78));
        var betTitle = Label(betPill.transform, "Title", "TOTAL BET");
        betTitle.fontSize = 16;
        betTitle.color = new Color(1f, 0.85f, 0.4f, 0.85f);
        Place(betTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(180, 22));
        var betAmountText = Label(betPill.transform, "Amount", "10,000");
        betAmountText.fontSize = 26;
        betAmountText.enableAutoSizing = true;
        betAmountText.fontSizeMin = 14;
        betAmountText.fontSizeMax = 26;
        betAmountText.textWrappingMode = TextWrappingModes.NoWrap;
        betAmountText.color = Color.white;
        Place(betAmountText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -14), new Vector2(180, 34));

        // Bet plus button
        var betPlus  = BtnText(deck, "BetPlus", "+", new Vector2(0.5f, 0.5f), new Vector2(-15, 0), new Vector2(50, 50));

        // MAX BET / AUTO / SPIN share one uniform pill size so the deck reads
        // as a consistent row instead of three mismatched buttons.
        var actionSize = new Vector2(150, 78);

        // MAX BET button
        var maxBetBtn = BtnPill(deck, "MaxBetBtn", "MAX BET", new Vector2(0.5f, 0.5f), new Vector2(100, 0), actionSize);

        // Auto spin button
        var autoBtn = BtnPill(deck, "AutoSpinBtn", "AUTO", new Vector2(0.5f, 0.5f), new Vector2(265, 0), actionSize);
        var autoDot = Img(autoBtn.transform, "Dot", null);
        autoDot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        autoDot.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        Place(autoDot.rectTransform, new Vector2(1f, 1f), new Vector2(-14, -14), new Vector2(16, 16));

        // Big golden mechanical SPIN button
        var spinBtn = BtnPill(deck, "SpinBtn", "SPIN", new Vector2(0.5f, 0.5f), new Vector2(430, 0), actionSize);

        // 8. Free Spins Banner (top banner that drops down)
        var fsBanner = Frame(safe, "FreeSpinsBanner", 24f, 6f, new Color(1f, 0.85f, 0.2f), new Color(0.6f, 0.08f, 0.05f), new Color(0.3f, 0.02f, 0.02f));
        Place(fsBanner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(620, 64));
        var fsText = Label(fsBanner.transform, "Text", "FREE SPINS: 10 (2X MULTIPLIER!)");
        fsText.fontSize = 28;
        fsText.color = GoldBright;
        Place(fsText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 50));
        fsBanner.gameObject.SetActive(false);

        // 9. Win Celebration Popup
        var winPopupGO = new GameObject("WinCelebrationPopup", typeof(RectTransform), typeof(SlotWinPopup));
        winPopupGO.transform.SetParent(canvasGO.transform, false);
        var winPopup = winPopupGO.GetComponent<SlotWinPopup>();
        Stretch(winPopupGO.GetComponent<RectTransform>());

        // Scrim
        var scrim = Img(winPopupGO.transform, "Scrim", null);
        scrim.sprite = null;
        scrim.preserveAspect = false;
        scrim.color = new Color(0, 0, 0, 0.8f);
        Stretch(scrim.rectTransform);

        // Celebration card
        var celCard = Frame(winPopupGO.transform, "Card", 32f, 8f, GoldBright, new Color(0.45f, 0.05f, 0.08f, 0.98f), new Color(0.15f, 0.01f, 0.02f, 0.98f));
        Place(celCard.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 420));
        winPopup.card = celCard.rectTransform;

        var celTitle = Label(celCard.transform, "Title", "BIG WIN!");
        celTitle.fontSize = 62;
        celTitle.color = GoldBright;
        Place(celTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(650, 90));
        winPopup.titleText = celTitle;

        var celAmount = Label(celCard.transform, "Amount", "1,000,000");
        celAmount.fontSize = 72;
        celAmount.color = Color.white;
        Place(celAmount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(650, 100));
        winPopup.winAmountText = celAmount;

        var collectBtn = BtnPill(celCard.transform, "CollectBtn", "COLLECT", new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(280, 74));
        winPopup.collectButton = collectBtn;
        collectBtn.onClick.AddListener(winPopup.Dismiss);
        winPopupGO.SetActive(false);

        // 10. Wire SlotMachine & SlotUI
        var machine = canvasGO.AddComponent<SlotMachine>();
        var ui = canvasGO.AddComponent<SlotUI>();

        machine.reels = reels;
        machine.ui = ui;
        machine.winPopup = winPopup;

        ui.machine = machine;
        ui.coinText = coinText;
        ui.coinIcon = Load(ArtUI + "Icon_Coin.png");
        ui.gemText = gemText;
        ui.backToLobbyBtn = backBtn;
        ui.grandText = grandText;
        ui.majorText = majorText;
        ui.minorText = minorText;
        ui.miniText = miniText;
        ui.spinBtn = spinBtn;
        ui.autoSpinBtn = autoBtn;
        ui.autoSpinIndicator = autoDot;
        ui.betMinusBtn = betMinus;
        ui.betPlusBtn = betPlus;
        ui.maxBetBtn = maxBetBtn;
        ui.betAmountText = betAmountText;
        ui.winAmountText = winAmountText;
        ui.freeSpinsBanner = fsBanner.gameObject;
        ui.freeSpinsCountText = fsText;

        // Explicit direct event wiring in builder for fail-safe runtime operation
        backBtn.onClick.AddListener(ui.OnBackToLobbyClicked);
        betMinus.onClick.AddListener(() => machine.ChangeBet(-1));
        betPlus.onClick.AddListener(() => machine.ChangeBet(1));
        maxBetBtn.onClick.AddListener(() => machine.SetMaxBet());
        autoBtn.onClick.AddListener(ui.OnAutoSpinClicked);
        spinBtn.onClick.AddListener(ui.OnSpinClicked);

        // Register symbols into machine (serialized array for runtime persistence)
        machine.symbolEntries = new[]
        {
            new SlotMachine.SymbolEntry { id = SymbolId.SevenRed,  sprite = Load(ArtSymbols + "Sym_7_Red.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.SevenGold, sprite = Load(ArtSymbols + "Sym_7_Gold.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Bar,       sprite = Load(ArtSymbols + "Sym_Bar.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Wild,      sprite = Load(ArtSymbols + "Sym_Wild.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Scatter,   sprite = Load(ArtSymbols + "Sym_Scatter.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Ace,       sprite = Load(ArtSymbols + "Sym_A.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.King,      sprite = Load(ArtSymbols + "Sym_K.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Queen,     sprite = Load(ArtSymbols + "Sym_Q.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Jack,      sprite = Load(ArtSymbols + "Sym_J.png") },
            new SlotMachine.SymbolEntry { id = SymbolId.Ten,       sprite = Load(ArtSymbols + "Sym_10.png") }
        };

        // 11. Register scenes in EditorBuildSettings
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SlotGame.unity", true)
        };

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SlotGame.unity");
        Debug.Log("SlotGame scene built and registered successfully!");
    }

    static TMP_Text JackpotPlate(Transform parent, string title, string barPath, string amount)
    {
        var bar = Img(parent, title + "Plate", barPath);
        bar.preserveAspect = true;
        var rt = bar.rectTransform;
        rt.sizeDelta = new Vector2(224, 62);

        var txt = Label(bar.transform, "Amount", amount);
        txt.fontSize = 24;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 11;
        txt.fontSizeMax = 24;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        txt.color = Color.white;
        // Narrower than the plate: the bar art's scrollwork end caps eat more
        // width than a plain rectangle, so a box sized to the full plate lets
        // digits spill past the painted border.
        Place(txt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(128, 38));
        return txt;
    }

    // Exactly matches LobbyBuilder.PillButton — same Btn_Gold.png art, so
    // every text button in the game (lobby and slot scene) reads as one
    // consistent widget.
    const float ButtonArtHeight = 562f;

    static Button BtnPill(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Graphic graphic;
        var plate = Load(ArtUI + "Btn_Gold.png");
        if (plate != null)
        {
            var img = Img(parent, name, ArtUI + "Btn_Gold.png");
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.raycastTarget = true;
            img.pixelsPerUnitMultiplier = ButtonArtHeight / Mathf.Max(1f, size.y);
            Place(img.rectTransform, anchor, pos, size);
            graphic = img;
        }
        else
        {
            var frame = Frame(parent, name, size.y * 0.5f, 6f, GoldBright, PillTop, PillBottom);
            Place(frame.rectTransform, anchor, pos, size);
            graphic = frame;
        }

        var lbl = Label(graphic.transform, "Text", text);
        lbl.fontSize = 24;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 10;
        lbl.fontSizeMax = 40;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        // Even at fontSizeMin, a label longer than the plate's painted end
        // caps allow would otherwise spill past the pill's rounded border.
        lbl.overflowMode = TextOverflowModes.Ellipsis;
        lbl.color = Color.white;
        Stretch(lbl.rectTransform);
        lbl.rectTransform.offsetMin = new Vector2(size.x * 0.14f, 0f);
        lbl.rectTransform.offsetMax = new Vector2(-size.x * 0.14f, 0f);
        lbl.raycastTarget = false;

        var btn = graphic.gameObject.AddComponent<Button>();
        btn.targetGraphic = graphic;
        return btn;
    }

    static Button BtnPill(Transform parent, string name, string text, Vector2 pos, Vector2 size)
    {
        return BtnPill(parent, name, text, new Vector2(0.5f, 0.5f), pos, size);
    }

    static Button BtnText(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        return BtnPill(parent, name, text, anchor, pos, size);
    }

    // Exactly matches LobbyBuilder.CurrencyPill
    static TMP_Text CurrencyPill(Transform parent, string name, string iconPath, string value,
        Vector2 pos, Vector2 size, Color rimColor)
    {
        var rim = Frame(parent, name, size.y * 0.5f, 10f, rimColor, PillFillTop, PillFillBottom);
        Place(rim.rectTransform, new Vector2(0, 0.5f), pos, size);

        var text = Label(rim.transform, "Value", value);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(size.y + 12, 0);
        rt.offsetMax = new Vector2(-size.y * 0.42f, 0);
        text.alignment = TextAlignmentOptions.Midline;
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

    static Sprite Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        Sprite best = null;
        float bestArea = -1f;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is not Sprite s) continue;
            float area = s.rect.width * s.rect.height;
            if (area > bestArea) { bestArea = area; best = s; }
        }
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
        if (LobbyBuilder.DisplayFont != null) t.font = LobbyBuilder.DisplayFont;
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    static ThemedFrame Frame(Transform parent, string name, float radius, float border,
        Color borderColor, Color fillTop, Color fillBottom)
    {
        var go = new GameObject(name, typeof(ThemedFrame));
        go.transform.SetParent(parent, false);
        var f = go.GetComponent<ThemedFrame>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/FrameSDF.mat");
        if (mat != null) f.material = mat;
        f.cornerRadius = radius;
        f.borderThickness = border;
        f.borderColor = borderColor;
        f.fillTop = fillTop;
        f.fillBottom = fillBottom;
        f.raycastTarget = false;
        return f;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
    }

    static void OnClick(Button b, UnityEngine.Events.UnityAction act)
    {
        if (b != null) b.onClick.AddListener(act);
    }
}
