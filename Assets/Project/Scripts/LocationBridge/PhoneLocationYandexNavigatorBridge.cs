using System.Net;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneLocationYandexNavigatorBridge : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhoneLocationUdpReceiver receiver;
    [SerializeField] private YandexHelmetNavigator navigator;

    [Header("Behavior")]
    [SerializeField] private bool applyEveryReceivedLocation = true;
    [SerializeField] private bool rebuildVisibleRouteOnLocation = true;

    public PhoneLocationSample? LatestAppliedSample { get; private set; }

    private void Awake()
    {
        if (receiver == null)
        {
            receiver = GetComponent<PhoneLocationUdpReceiver>();
        }

        if (navigator == null)
        {
            navigator = GetComponent<YandexHelmetNavigator>();
        }
    }

    private void OnEnable()
    {
        if (receiver != null)
        {
            receiver.LocationReceived += OnLocationReceived;
        }
    }

    private void OnDisable()
    {
        if (receiver != null)
        {
            receiver.LocationReceived -= OnLocationReceived;
        }
    }

    public bool ApplyLatestLocation()
    {
        if (receiver == null || !receiver.HasLocation)
        {
            return false;
        }

        return Apply(receiver.LatestSample);
    }

    public bool Apply(PhoneLocationSample sample)
    {
        if (navigator == null || !sample.IsValid)
        {
            return false;
        }

        navigator.SetCurrentCoordinate(new GeoCoordinate(sample.Latitude, sample.Longitude));
        LatestAppliedSample = sample;

        if (rebuildVisibleRouteOnLocation && navigator.IsRouteVisible)
        {
            navigator.TryRebuildRouteFromCurrentCoordinate();
        }

        return true;
    }

    private void OnLocationReceived(PhoneLocationSample sample, IPEndPoint remoteEndPoint)
    {
        if (applyEveryReceivedLocation)
        {
            Apply(sample);
        }
    }
}
