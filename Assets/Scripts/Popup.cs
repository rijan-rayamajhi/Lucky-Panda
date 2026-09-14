using UnityEngine;

// Minimal show/hide popup. `root` is the panel GameObject this lives on; it
// starts inactive (set in the scene) and Open/Close toggle it. Callers hold the
// component reference, so Open works even while the root is inactive.
public class Popup : MonoBehaviour
{
    public GameObject root;

    public void Open()  { if (root) root.SetActive(true); }
    public void Close() { if (root) root.SetActive(false); }
    public void Toggle() { if (root) root.SetActive(!root.activeSelf); }
}
