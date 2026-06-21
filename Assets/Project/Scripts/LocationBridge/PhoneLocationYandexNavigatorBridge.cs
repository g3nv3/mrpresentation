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
    [SerializeField, Min(0f)] private float minimumRebuildDistanceMeters = 3f;
    [SerializeField, Min(0f)] private float minimumRebuildIntervalSeconds = 2f;
    [SerializeField, Min(0f)] private float minimumAlignmentSpeedMetersPerSecond = 0.4f;
    [SerializeField, Min(0f)] private float maximumHeadingAccuracyDegrees = 45f;

    public PhoneLocationSample? LatestAppliedSample { get; private set; }

    private GeoCoordinate? _lastRouteCoordinate;
    private float _lastRebuildTime = float.NegativeInfinity;

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

        var coordinate = new GeoCoordinate(sample.Latitude, sample.Longitude);
        navigator.SetCurrentCoordinate(coordinate);
        LatestAppliedSample = sample;

        if (navigator.IsRouteVisible && !navigator.HasGeographicNorthAlignment)
        {
            TryAlignGeographicNorthFromLatestHeading();
        }

        if (rebuildVisibleRouteOnLocation && navigator.IsRouteVisible && ShouldRebuild(coordinate))
        {
            if (navigator.TryRebuildRouteFromCurrentCoordinate())
            {
                _lastRouteCoordinate = coordinate;
                _lastRebuildTime = Time.unscaledTime;
            }
        }

        return true;
    }

    public bool TryAlignGeographicNorthFromLatestCourse()
    {
        if (!LatestAppliedSample.HasValue)
        {
            return false;
        }

        var sample = LatestAppliedSample.Value;
        return sample.CourseDegrees >= 0f &&
               sample.SpeedMetersPerSecond >= minimumAlignmentSpeedMetersPerSecond &&
               navigator != null &&
               navigator.TryAlignGeographicNorthToCourse(sample.CourseDegrees);
    }

    public bool TryAlignGeographicNorthFromLatestHeading(bool force = false)
    {
        if (!LatestAppliedSample.HasValue || navigator == null)
        {
            return false;
        }

        var sample = LatestAppliedSample.Value;
        if (!sample.HasHeading || sample.HeadingDegrees < 0f || sample.HeadingDegrees >= 360f)
        {
            return false;
        }

        if (sample.HeadingAccuracyDegrees >= 0f &&
            sample.HeadingAccuracyDegrees > maximumHeadingAccuracyDegrees)
        {
            return false;
        }

        return force
            ? navigator.TryAlignGeographicNorthToHeading(sample.HeadingDegrees)
            : navigator.TryAlignGeographicNorthToCourse(sample.HeadingDegrees);
    }

    private bool ShouldRebuild(GeoCoordinate coordinate)
    {
        if (navigator.IsRequesting ||
            Time.unscaledTime - _lastRebuildTime < minimumRebuildIntervalSeconds)
        {
            return false;
        }

        return !_lastRouteCoordinate.HasValue ||
               GeoCoordinateUtility.GetMetersOffset(_lastRouteCoordinate.Value, coordinate).magnitude >=
               minimumRebuildDistanceMeters;
    }

    private void OnLocationReceived(PhoneLocationSample sample, IPEndPoint remoteEndPoint)
    {
        if (applyEveryReceivedLocation)
        {
            Apply(sample);
        }
    }
}
