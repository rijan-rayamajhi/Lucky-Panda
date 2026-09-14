using UnityEngine;
using TMPro;

// Count badge on a bottom-nav icon. Pulls its number from Notifications, which
// asks each service directly, so nothing has to remember to push an update.
public class NavBadge : MonoBehaviour
{
    public NavTab tab;
    public GameObject badgeRoot;
    public TMP_Text countText;

    void OnEnable()
    {
        Notifications.Changed -= Refresh;
        Notifications.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        Notifications.Changed -= Refresh;
    }

    public void Refresh()
    {
        int n = Notifications.CountFor(tab);
        if (badgeRoot != null) badgeRoot.SetActive(n > 0);
        if (countText != null) countText.text = n > 99 ? "99+" : n.ToString();
    }
}
