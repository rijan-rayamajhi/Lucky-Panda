using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PuzzlePanel : Popup
{
    public RectTransform grid;
    public TMP_Text titleText;
    public TMP_Text progressText;
    public TMP_Text hintText;
    public Button claimButton;

    [Tooltip("Pixel size of the square the grid is drawn into.")]
    public float boardSize = 520f;

    readonly List<Sprite> sliced = new List<Sprite>();

    void Awake()
    {
        if (claimButton) claimButton.onClick.AddListener(OnClaim);
    }

    void OnEnable() => Rebuild();

    void OnDisable() => ReleaseSlices();

    void OnClaim()
    {
        if (PuzzleService.ClaimComplete()) Rebuild();
    }

    void Rebuild()
    {
        if (grid == null) return;

        UIFactory.Clear(grid);
        ReleaseSlices();

        if (PuzzleService.AllComplete)
        {
            if (titleText) titleText.text = "ALL PUZZLES COMPLETE";
            if (progressText) progressText.text = "";
            if (hintText) hintText.text = "More boards coming soon.";
            UIFactory.SetPillState(claimButton, false, "COMPLETE");
            return;
        }

        var def = PuzzleService.Current;
        int rows = Mathf.Max(1, def.rows);
        int cols = Mathf.Max(1, def.cols);
        int owned = PuzzleService.OwnedCount;
        int total = def.PieceCount;

        if (titleText) titleText.text = def.title;
        if (progressText) progressText.text = owned + " / " + total;

        var layout = grid.GetComponent<GridLayoutGroup>();
        if (layout == null) layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        const float gap = 6f;
        float cell = (boardSize - gap * (cols - 1)) / cols;
        layout.cellSize = new Vector2(cell, cell);
        layout.spacing = new Vector2(gap, gap);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = cols;
        layout.childAlignment = TextAnchor.MiddleCenter;

        var source = ContentRefs.PuzzleImage(def.imageIndex);

        for (int i = 0; i < total; i++)
        {
            int row = i / cols;
            int col = i % cols;
            bool has = PuzzleService.HasPiece(i);

            if (has && source != null)
            {
                var piece = Slice(source, row, col, rows, cols);
                var img = UIFactory.Img(grid, "Piece" + i, piece);
                img.preserveAspect = false;
            }
            else if (has)
            {
                // No artwork supplied yet: a solid tinted cell still reads as
                // "you own this one" against the dark empty cells.
                var solid = UIFactory.Frame(grid, "Piece" + i, 4f, 2f, UIFactory.Gold,
                    Color.Lerp(UIFactory.BarTop, UIFactory.BarBottom, i / (float)total),
                    Color.Lerp(UIFactory.BarBottom, UIFactory.GreenTop, i / (float)total));
                var num = UIFactory.Label(solid.transform, "N", (i + 1).ToString(), 22);
                UIFactory.Stretch(num.rectTransform);
                num.color = new Color(0f, 0f, 0f, 0.45f);
            }
            else
            {
                var empty = UIFactory.Frame(grid, "Empty" + i, 6f, 2f,
                    new Color(0.42f, 0.32f, 0.18f, 0.75f),
                    new Color(0.13f, 0.08f, 0.21f, 1f),
                    new Color(0.06f, 0.03f, 0.11f, 1f));
                var q = UIFactory.Label(empty.transform, "Q", "?", 46);
                UIFactory.Stretch(q.rectTransform);
                q.color = new Color(0.52f, 0.42f, 0.24f, 1f);
            }
        }

        bool complete = PuzzleService.IsComplete;
        UIFactory.SetPillState(claimButton, complete, complete ? "CLAIM REWARD" : "KEEP COLLECTING");

        if (hintText)
        {
            int banked = PuzzleService.Banked;
            if (complete)
                hintText.text = "BOARD FULL — CLAIM " + def.reward.Describe();
            else if (banked > 0)
                hintText.text = banked + " piece" + (banked == 1 ? "" : "s") + " waiting for the next board";
            else
                hintText.text = "Pieces come from quests, card sets and the wheel";
        }

        AudioManager.HookChildren(gameObject);
    }

    // Sprite.Create references a sub-rectangle; it never reads pixels, so the
    // source texture does not need Read/Write enabled.
    Sprite Slice(Sprite source, int row, int col, int rows, int cols)
    {
        var tr = source.textureRect;
        float w = tr.width / cols;
        float h = tr.height / rows;
        // Texture space runs bottom-up; the grid is laid out top-down.
        var rect = new Rect(tr.x + col * w, tr.y + (rows - 1 - row) * h, w, h);
        var piece = Sprite.Create(source.texture, rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        piece.name = "slice_" + row + "_" + col;
        sliced.Add(piece);
        return piece;
    }

    void ReleaseSlices()
    {
        for (int i = 0; i < sliced.Count; i++)
            if (sliced[i] != null) Destroy(sliced[i]);
        sliced.Clear();
    }
}
