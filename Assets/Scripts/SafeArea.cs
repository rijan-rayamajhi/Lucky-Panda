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

    void Update()
    {
        // Rotation and foldables change the cutout at runtime.
        if (Screen.safeArea != applied || Screen.orientation != appliedOrientation) Apply();
    }

    void Apply()
    {
        applied = Screen.safeArea;
        appliedOrientation = Screen.orientation;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        var min = applied.position;
        var max = applied.position + applied.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
