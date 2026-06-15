using System.Net;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneLocationDebugLogger : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private PhoneGpsLocationProvider gpsProvider;
    [SerializeField] private PhoneLocationUdpBroadcaster broadcaster;
    [SerializeField] private PhoneLocationUdpReceiver receiver;
    [SerializeField] private PhoneLocationYandexNavigatorBridge yandexBridge;

    [Header("Logging")]
    [SerializeField] private bool logOnEnable = true;
    [SerializeField] private bool logGpsUpdates = true;
    [SerializeField] private bool logSentPackets = true;
    [SerializeField] private bool logReceivedPackets = true;
    [SerializeField] private bool logErrors = true;
    [SerializeField] private bool logPeriodicStatus = true;
    [SerializeField, Min(0.25f)] private float statusIntervalSeconds = 2f;
    [SerializeField] private string logTag = "[PhoneLocation]";

    private float _nextStatusTime;

    private void Awake()
    {
        ResolveSources();
    }

    private void OnEnable()
    {
        ResolveSources();
        Subscribe();

        if (logOnEnable)
        {
            LogStatus("enabled");
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        Log("disabled");
    }

    private void Update()
    {
        if (!logPeriodicStatus || Time.unscaledTime < _nextStatusTime)
        {
            return;
        }

        _nextStatusTime = Time.unscaledTime + statusIntervalSeconds;
        LogStatus("periodic");
    }

    public void LogStatusNow()
    {
        LogStatus("manual");
    }

    public void SetPeriodicStatusLogging(bool enabled)
    {
        logPeriodicStatus = enabled;
    }

    private void ResolveSources()
    {
        if (gpsProvider == null)
        {
            gpsProvider = GetComponent<PhoneGpsLocationProvider>();
        }

        if (broadcaster == null)
        {
            broadcaster = GetComponent<PhoneLocationUdpBroadcaster>();
        }

        if (receiver == null)
        {
            receiver = GetComponent<PhoneLocationUdpReceiver>();
        }

        if (yandexBridge == null)
        {
            yandexBridge = GetComponent<PhoneLocationYandexNavigatorBridge>();
        }
    }

    private void Subscribe()
    {
        if (gpsProvider != null)
        {
            gpsProvider.LocationUpdated += OnGpsLocationUpdated;
            gpsProvider.Failed += OnFailure;
        }

        if (broadcaster != null)
        {
            broadcaster.PacketSent += OnPacketSent;
            broadcaster.Failed += OnFailure;
        }

        if (receiver != null)
        {
            receiver.LocationReceived += OnLocationReceived;
            receiver.Failed += OnFailure;
        }
    }

    private void Unsubscribe()
    {
        if (gpsProvider != null)
        {
            gpsProvider.LocationUpdated -= OnGpsLocationUpdated;
            gpsProvider.Failed -= OnFailure;
        }

        if (broadcaster != null)
        {
            broadcaster.PacketSent -= OnPacketSent;
            broadcaster.Failed -= OnFailure;
        }

        if (receiver != null)
        {
            receiver.LocationReceived -= OnLocationReceived;
            receiver.Failed -= OnFailure;
        }
    }

    private void OnGpsLocationUpdated(PhoneLocationSample sample)
    {
        if (logGpsUpdates)
        {
            Log("gps update " + FormatSample(sample));
        }
    }

    private void OnPacketSent(PhoneLocationSample sample)
    {
        if (logSentPackets)
        {
            Log("udp sent " + FormatSample(sample));
        }
    }

    private void OnLocationReceived(PhoneLocationSample sample, IPEndPoint remoteEndPoint)
    {
        if (logReceivedPackets)
        {
            Log("udp received from " + remoteEndPoint + " " + FormatSample(sample));
        }
    }

    private void OnFailure(string error)
    {
        if (logErrors)
        {
            Debug.LogWarning(logTag + " error " + error, this);
        }
    }

    private void LogStatus(string reason)
    {
        var gpsStatus = gpsProvider != null
            ? "gps(status=" + gpsProvider.Status +
              ", has=" + gpsProvider.HasLocation +
              ", running=" + gpsProvider.IsRunning +
              ", starting=" + gpsProvider.IsStarting + ")"
            : "gps(null)";

        var broadcasterStatus = broadcaster != null
            ? "broadcaster(active=" + broadcaster.IsBroadcasting +
              ", target=" + broadcaster.TargetAddress +
              ":" + broadcaster.TargetPort + ")"
            : "broadcaster(null)";

        var receiverStatus = receiver != null
            ? "receiver(active=" + receiver.IsListening +
              ", port=" + receiver.ListenPort +
              ", has=" + receiver.HasLocation +
              ", fresh=" + receiver.IsLatestLocationFresh + ")"
            : "receiver(null)";

        var bridgeStatus = yandexBridge != null
            ? "bridge(applied=" + yandexBridge.LatestAppliedSample.HasValue + ")"
            : "bridge(null)";

        Log("status " + reason + " " + gpsStatus + " " + broadcasterStatus + " " + receiverStatus + " " + bridgeStatus);

        if (gpsProvider != null && gpsProvider.HasLocation)
        {
            Log("latest gps " + FormatSample(gpsProvider.LastSample));
        }

        if (receiver != null && receiver.HasLocation)
        {
            Log("latest received " + FormatSample(receiver.LatestSample) + " from " + receiver.LatestRemoteEndPoint);
        }
    }

    private void Log(string message)
    {
        Debug.Log(logTag + " " + message, this);
    }

    private static string FormatSample(PhoneLocationSample sample)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "seq={0} device={1} lat={2:F7} lon={3:F7} alt={4:F1} hAcc={5:F1} vAcc={6:F1} speed={7:F2} course={8:F1} ts={9:F3}",
            sample.Sequence,
            sample.DeviceId,
            sample.Latitude,
            sample.Longitude,
            sample.AltitudeMeters,
            sample.HorizontalAccuracyMeters,
            sample.VerticalAccuracyMeters,
            sample.SpeedMetersPerSecond,
            sample.CourseDegrees,
            sample.Timestamp);
    }
}
