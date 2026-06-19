using UnityEngine;

public sealed class QrMarkerInstallNotificationOptions
{
    private QrMarkerInstallNotificationPanel panel;

    public QrMarkerInstallNotificationOptions(QrMarkerInstallNotificationPanel panel)
    {
        this.panel = panel;
    }

    public QrMarkerInstallNotificationPanel Panel
    {
        get
        {
            if (panel == null)
            {
                panel = Object.FindFirstObjectByType<QrMarkerInstallNotificationPanel>(FindObjectsInactive.Include);
            }

            return panel;
        }
    }
}
