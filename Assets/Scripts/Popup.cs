using UnityEngine;

// Minimal show/hide popup. `root` is the panel GameObject this lives on; it
// starts inactive (set in the scene) and Open/Close toggle it. Callers hold the
// component reference, so Open works even while the root is inactive.
public class Popup : MonoBehaviour
{
    public GameObject root;

    public void Open()
    {
        if (root == null) return;
        root.SetActive(true);
        SetNavVisible(false);
        // Panels build their contents in OnEnable, so hook after activation.
        AudioManager.HookChildren(root);
    }

    public void Close()
    {
        if (root) root.SetActive(false);
        SetNavVisible(true);
    }

    public void Toggle()
    {
        if (root == null) return;
        bool next = !root.activeSelf;
        root.SetActive(next);
        SetNavVisible(!next);
        if (next) AudioManager.HookChildren(root);
    }

    static GameObject cachedNav;
    static void SetNavVisible(bool visible)
    {
        if (cachedNav == null)
        {
            var nav = GameObject.Find("BottomNav");
            if (nav != null) cachedNav = nav;
            else
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "BottomNav" && all[i].scene.isLoaded)
                    {
                        cachedNav = all[i];
                        break;
                    }
                }
            }
        }

        if (cachedNav != null)
        {
            var cg = cachedNav.GetComponent<CanvasGroup>();
            if (cg == null) cg = cachedNav.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }
    }
}
