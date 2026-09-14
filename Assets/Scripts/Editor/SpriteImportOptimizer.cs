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
        { "BG_Lobby",          2048 },
        // Large panels; 512 visibly softens their painted detail.
        { "Panel_Popup",       1024 },
        { "Panel_Popup2",      1024 },
        { "Bubble_Speech",     1024 },
        { "Host_Hero",         1024 },
        // Drawn large and full of fine gold detail; 256 turns it to mush.
        { "Wheel_Face",        1024 },
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

    static readonly string[] Folders =
    {
        "Assets/Art/UI",
        "Assets/Art/Symbols",
        "Assets/Art/Characters",
        "Assets/Art/Backgrounds",
    };

    [MenuItem("Lucky Panda/Optimize Sprite Imports")]
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
                var name = Path.GetFileNameWithoutExtension(path);

                int max = PerFile.TryGetValue(name, out var v) ? v
                        : folder.EndsWith("Symbols") ? SymbolSize
                        : DefaultIconSize;

                if (Apply(path, max)) changed++;
            }
        }

        BuildAtlases();
        AssetDatabase.Refresh();
        Debug.Log($"Sprite imports: {seen} scanned, {changed} reimported.");
    }

    // Cheap pass that only repairs the import mode, so the scene builder can
    // guarantee no sprite lookup returns an auto-slice fragment.
    public static void EnsureSingleSpriteMode()
    {
        int repaired = 0;
        foreach (var folder in Folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.GetFiles(folder, "*.png"))
            {
                var path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (dirty) { importer.SaveAndReimport(); repaired++; }
            }
        }
        if (repaired > 0) Debug.Log($"Sprite mode: {repaired} texture(s) reset to Single.");
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
    [MenuItem("Lucky Panda/Rebuild Sprite Atlases")]
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

        var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
        if (folderAsset != null) atlas.Add(new Object[] { folderAsset });

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

        Debug.Log($"Atlas ready: {path}");
    }
}
