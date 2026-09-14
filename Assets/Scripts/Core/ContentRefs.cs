using UnityEngine;
using TMPro;

// Asset handles the scene builder fills in, so runtime code can build rows and
// cards without AssetDatabase (editor-only) or Resources.Load (easy to break
// by moving a file). Lives on the Canvas, which is always active, so Awake is
// guaranteed to run before any panel opens.
public class ContentRefs : MonoBehaviour
{
    public static ContentRefs I;

    [Header("Currency")]
    public Sprite iconCoin;
    public Sprite iconGem;
    public Sprite iconGift;

    [Header("Panels")]
    public Sprite panelCard;
    public Sprite closeButton;
    public Sprite barMajor;
    public Sprite barGrand;

    [Header("Ornate plates")]
    [Tooltip("9-sliced button plate. Null falls back to the procedural pill.")]
    public Sprite buttonPlate;
    [Tooltip("9-sliced list row plate. Null falls back to a ThemedFrame.")]
    public Sprite rowPlate;
    [Tooltip("Hollow card border drawn over the card contents.")]
    public Sprite cardFrame;

    [Header("Cards")]
    public Sprite[] cardSymbols;

    [Header("Puzzle")]
    [Tooltip("Source images sliced into puzzle pieces. Missing entries fall back to coloured cells.")]
    public Sprite[] puzzleImages;

    [Header("Shared")]
    public Material frameMaterial;
    public TMP_FontAsset displayFont;

    void Awake()
    {
        if (I == null) I = this;
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    public static Sprite Symbol(int index)
    {
        var refs = I;
        if (refs == null || refs.cardSymbols == null || refs.cardSymbols.Length == 0) return null;
        return refs.cardSymbols[((index % refs.cardSymbols.Length) + refs.cardSymbols.Length) % refs.cardSymbols.Length];
    }

    public static Sprite PuzzleImage(int index)
    {
        var refs = I;
        if (refs == null || refs.puzzleImages == null || refs.puzzleImages.Length == 0) return null;
        return refs.puzzleImages[((index % refs.puzzleImages.Length) + refs.puzzleImages.Length) % refs.puzzleImages.Length];
    }
}
