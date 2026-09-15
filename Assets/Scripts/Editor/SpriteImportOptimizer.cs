using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.U2D;
using UnityEditor;
using UnityEditor.U2D;

public static class SpriteImportOptimizer
{
    // Budgets are the next power of two above how large each sprite actually
    // draws on a 1920x1080 canvas. Capping everything at one value either
    // blurs the big elements or wastes memory on the small ones.
    static readonly Dictionary<string, int> PerFile = new()
    {
        // Drawn full-screen.
        { "BG_Lobby",           2048 },
        { "Bg_Slot777",         2048 },
        { "Bg_TripleDiamond",   2048 },
        { "Bg_FruitFortune",    2048 },
        // Large panels; 512 visibly softens their painted detail.
        { "Panel_Popup",       1024 },
        { "Panel_Popup2",      1024 },
        { "Bubble_Speech",     1024 },
        { "Host_Hero",         1024 },
        // Drawn large and full of fine gold detail; 256 turns it to mush.
        { "Wheel_Face",        1024 },
        // Slot cabinets, drawn 920px wide with fine filigree.
        { "Frame_Classic777",     1024 },
        { "Frame_TripleDiamond",  1024 },
        { "Frame_FruitFortune",   1024 },
        // Lobby tiles and their shared border, drawn ~320px wide.
        { "Card_Slot777",          512 },
        { "Card_TripleDiamond",    512 },
        { "Card_FruitFortune",     512 },
        { "Frame_SlotGame",        512 },
        // Mid-size stretched chrome.
        { "Pill_Currency",      512 },
        { "Bar_ProgressFrame",  512 },
        { "Bar_ProgressFill",   512 },
        { "Bar_Grand",          512 },
        { "Bar_Major",          512 },
        { "Bar_Minor",          512 },
        { "Bar_Mini",           512 },
    };

    // Anything not named above: icons, nav buttons, small badges.
    const int DefaultIconSize = 256;
    const int SymbolSize = 512;

    /// Only sprites budgeted at or below this go in an atlas. Batching pays off
    /// for the many small repeated icons; the big single-draw pieces (cabinet
    /// frames, popups, the wheel face) gain nothing and, packed together,
    /// overflow the atlas pages so the packer downscales them — which is how
    /// the art lost resolution once the second machine's frame was added.
    const int MaxAtlasSourceSize = 512;

    static readonly string[] Folders =
    {
        "Assets/Art/UI",
        "Assets/Art/Symbols",
        "Assets/Art/Characters",
        "Assets/Art/Backgrounds",
    };

    static int BudgetFor(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (PerFile.TryGetValue(name, out var v)) return v;
        return path.Contains("/Symbols/") ? SymbolSize : DefaultIconSize;
    }

    /// One pass that leaves every texture import correct: sprite mode, size
    /// budget, compression, then atlases packed from whatever is small enough
    /// to benefit. Build Everything runs this, so freshly dropped art can't
    /// ship at the wrong resolution.
    [MenuItem("Lucky Panda/Dev/Optimize Sprite Imports")]
    public static void Optimize()
    {
        int seen = 0, changed = 0;
        foreach (var folder in Folders)
        {
            if (!Directory.Exists(folder)) { Debug.LogWarning("Missing: " + folder); continue; }
            foreach (var file in Directory.GetFiles(folder, "*.png"))
            {
                seen++;
                var path = file.Replace('\\', '/');
                if (Apply(path, BudgetFor(path))) changed++;
            }
        }

        BuildAtlases();
        AssetDatabase.Refresh();
        Debug.Log($"Sprite imports: {seen} scanned, {changed} reimported.");
    }

    static bool Apply(string path, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool dirty = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        // Auto-slicing leaves a pile of junk sub-sprites behind. Loading such a
        // texture by path returns whichever sub-sprite happens to come first —
        // often a few-pixel scrap — which is how stray UI fragments end up on
        // screen. Every texture here is a single image.
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
        if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            dirty = true;
        }
        // Shrinks build size substantially; costs import time, not runtime.
        if (!importer.crunchedCompression)
        {
            importer.crunchedCompression = true;
            dirty = true;
        }
        // Crunch at 50 visibly blocks up the gold gradients on this UI art and
        // reads as speckle/fragments at icon sizes. The size saving isn't worth
        // it for a handful of textures.
        if (importer.compressionQuality != 100)
        {
            importer.compressionQuality = 100;
            dirty = true;
        }

        if (dirty) importer.SaveAndReimport();
        return dirty;
    }

    // Each unatlased sprite is its own draw call; batching the UI collapses
    // the whole HUD into a couple of them.
    [MenuItem("Lucky Panda/Dev/Rebuild Sprite Atlases")]
    public static void BuildAtlases()
    {
        CreateAtlas("UI", "Assets/Art/UI");
        CreateAtlas("Symbols", "Assets/Art/Symbols");
        AssetDatabase.SaveAssets();
    }

    static void CreateAtlas(string name, string folder)
    {
        const string dir = "Assets/Art/Atlases";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string path = $"{dir}/{name}.spriteatlasv2";
        // Sprite Atlas V2's imported main asset is a SpriteAtlas, not a
        // SpriteAtlasAsset, so LoadAssetAtPath<SpriteAtlasAsset> always returns
        // null (that null was the NPE below). Build a fresh asset each run and
        // let Save overwrite the file.
        var atlas = new SpriteAtlasAsset();
        atlas.SetIncludeInBuild(true);

        var packing = atlas.GetPackingSettings();
        // Rotation and tight packing must both stay off for atlases consumed by
        // uGUI Image: the packer will otherwise turn sprites 90 degrees to fit,
        // and the UI draws them rotated. Padding keeps neighbours from bleeding
        // into each other once the atlas is block-compressed.
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        packing.padding = 8;
        atlas.SetPackingSettings(packing);

        var texSettings = atlas.GetTextureSettings();
        texSettings.generateMipMaps = false;
        atlas.SetTextureSettings(texSettings);

        // Adding the folder would sweep in the oversized art too. Add the
        // individual textures that are actually small enough to pack, so the
        // packer never has to shrink anything to make a page fit.
        var packed = new List<Object>();
        var skipped = new List<string>();
        foreach (var file in Directory.GetFiles(folder, "*.png"))
        {
            var p = file.Replace('\\', '/');
            if (BudgetFor(p) > MaxAtlasSourceSize) { skipped.Add(Path.GetFileNameWithoutExtension(p)); continue; }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex != null) packed.Add(tex);
        }
        if (packed.Count > 0) atlas.Add(packed.ToArray());

        SpriteAtlasAsset.Save(atlas, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        // Ensure the SpriteAtlasImporter serialized in .meta has rotation and tight packing disabled
        var importer = AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            var so = new SerializedObject(importer);
            var packProp = so.FindProperty("m_PackingSettings");
            if (packProp != null)
            {
                var rot = packProp.FindPropertyRelative("enableRotation");
                if (rot != null) rot.boolValue = false;
                var tight = packProp.FindPropertyRelative("enableTightPacking");
                if (tight != null) tight.boolValue = false;
                var pad = packProp.FindPropertyRelative("padding");
                if (pad != null) pad.intValue = 8;
                so.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
        }

        string note = skipped.Count > 0
            ? $" ({skipped.Count} drawn unatlased at full resolution: {string.Join(", ", skipped)})"
            : "";
        Debug.Log($"Atlas {name}: packed {packed.Count} sprite(s){note}");
    }
}
