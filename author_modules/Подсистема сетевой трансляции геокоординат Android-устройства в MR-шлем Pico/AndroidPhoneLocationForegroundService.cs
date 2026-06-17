using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public static class AndroidPhoneLocationForegroundService
{
    private const string FineLocationPermission = "android.permission.ACCESS_FINE_LOCATION";
    private const string CoarseLocationPermission = "android.permission.ACCESS_COARSE_LOCATION";
    private const string PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";
    private const string BridgeClassName =
        "com.phonepicoprovider.locationbridge.PhoneLocationForegroundServiceBridge";

    public static bool IsSupported
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static bool HasRequiredPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return HasLocationPermission() && HasNotificationPermissionIfRequired();
#else
        return true;
#endif
    }

    public static void RequestMissingPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!HasLocationPermission())
        {
            Permission.RequestUserPermission(FineLocationPermission);
        }

        if (!HasNotificationPermissionIfRequired())
        {
            Permission.RequestUserPermission(PostNotificationsPermission);
        }
#endif
    }

    public static void Start(
        string targetAddress,
        int targetPort,
        float sendIntervalSeconds,
        float updateDistanceMeters,
        string deviceId,
        bool allowBroadcast,
        bool keepCpuAwake)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var bridge = new AndroidJavaClass(BridgeClassName);
        using var activity = GetCurrentActivity();
        bridge.CallStatic(
            "start",
            activity,
            targetAddress,
            targetPort,
            sendIntervalSeconds,
            updateDistanceMeters,
            deviceId,
            allowBroadcast,
            keepCpuAwake);
#endif
    }

    public static void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var bridge = new AndroidJavaClass(BridgeClassName);
        using var activity = GetCurrentActivity();
        bridge.CallStatic("stop", activity);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool HasLocationPermission()
    {
        return Permission.HasUserAuthorizedPermission(FineLocationPermission) ||
               Permission.HasUserAuthorizedPermission(CoarseLocationPermission);
    }

    private static bool HasNotificationPermissionIfRequired()
    {
        return SdkInt < 33 || Permission.HasUserAuthorizedPermission(PostNotificationsPermission);
    }

    private static int SdkInt
    {
        get
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }
    }

    private static AndroidJavaObject GetCurrentActivity()
    {
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    }
#endif
}
