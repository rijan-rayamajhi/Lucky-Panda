using UnityEngine;

// Keeps a panel inside the device's usable area so the notch, rounded
// corners and home indicator never sit on top of interactive UI.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rt;
    Rect applied;
    ScreenOrientation appliedOrientation;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void OnEnable()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        // Rotation and foldables change the cutout at runtime.
        if (Screen.safeArea != applied || Screen.orientation != appliedOrientation) Apply();
    }

    void Apply()
    {
        if (!Application.isPlaying && !Application.isEditor) return;

        applied = Screen.safeArea;
        appliedOrientation = Screen.orientation;

        float sw = Screen.width;
        float sh = Screen.height;
        if (sw <= 100 || sh <= 100) return;

        var min = applied.position;
        var max = applied.position + applied.size;

        float minX = Mathf.Clamp01(min.x / sw);
        float minY = Mathf.Clamp01(min.y / sh);
        float maxX = Mathf.Clamp01(max.x / sw);
        float maxY = Mathf.Clamp01(max.y / sh);

        if (maxX <= minX || maxY <= minY)
        {
            minX = 0f; minY = 0f; maxX = 1f; maxY = 1f;
        }

        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

