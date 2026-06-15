using System;
using System.Collections;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

[DisallowMultipleComponent]
public sealed class PhoneGpsLocationProvider : MonoBehaviour
{
    private const string FineLocationPermission = "android.permission.ACCESS_FINE_LOCATION";
    private const string CoarseLocationPermission = "android.permission.ACCESS_COARSE_LOCATION";

    [Header("Service")]
    [SerializeField] private bool startOnEnable = true;
    [SerializeField, Min(1f)] private float desiredAccuracyMeters = 5f;
    [SerializeField, Min(0f)] private float updateDistanceMeters = 1f;
    [SerializeField, Min(1f)] private float initializationTimeoutSeconds = 20f;
    [SerializeField, Min(1f)] private float permissionTimeoutSeconds = 20f;

    [Header("Identity")]
    [SerializeField] private string deviceIdOverride;

    private Coroutine _startRoutine;
    private PhoneLocationSample _lastSample;
    private PhoneLocationSample _previousSample;
    private bool _hasLocation;
    private int _sequence;

    public event Action<PhoneLocationSample> LocationUpdated;
    public event Action<string> Failed;

    public bool HasLocation => _hasLocation;
    public bool IsStarting => _startRoutine != null;
    public bool IsRunning => Input.location.status == LocationServiceStatus.Running;
    public LocationServiceStatus Status => Input.location.status;
    public PhoneLocationSample LastSample => _lastSample;

    private string DeviceId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(deviceIdOverride))
            {
                return deviceIdOverride;
            }

            return SystemInfo.deviceUniqueIdentifier;
        }
    }

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartLocationService();
        }
    }

    private void OnDisable()
    {
        StopLocationService();
    }

    private void Update()
    {
        if (Input.location.status != LocationServiceStatus.Running)
        {
            return;
        }

        var location = Input.location.lastData;
        if (_hasLocation && Math.Abs(location.timestamp - _lastSample.Timestamp) < 0.0001d)
        {
            return;
        }

        _previousSample = _lastSample;
        _lastSample = BuildSample(location);
        _hasLocation = true;
        LocationUpdated?.Invoke(_lastSample);
    }

    public void StartLocationService()
    {
        if (_startRoutine != null || Input.location.status == LocationServiceStatus.Running)
        {
            return;
        }

        _startRoutine = StartCoroutine(StartLocationRoutine());
    }

    public void StopLocationService()
    {
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }

        if (Input.location.status == LocationServiceStatus.Running ||
            Input.location.status == LocationServiceStatus.Initializing)
        {
            Input.location.Stop();
        }
    }

    public void SetDeviceId(string deviceId)
    {
        deviceIdOverride = deviceId;
    }

    private IEnumerator StartLocationRoutine()
    {
        yield return RequestLocationPermissionRoutine();

        if (!HasLocationPermission())
        {
            Fail("Location permission was not granted.");
            yield break;
        }

        if (!Input.location.isEnabledByUser)
        {
            Fail("Location service is disabled by user.");
            yield break;
        }

        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

        var remaining = initializationTimeoutSeconds;
        while (Input.location.status == LocationServiceStatus.Initializing && remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Fail("Location service failed to start. Status: " + Input.location.status);
            yield break;
        }

        _startRoutine = null;
    }

    private IEnumerator RequestLocationPermissionRoutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(FineLocationPermission) &&
            !Permission.HasUserAuthorizedPermission(CoarseLocationPermission))
        {
            Permission.RequestUserPermission(FineLocationPermission);
        }

        var remaining = permissionTimeoutSeconds;
        while (!HasLocationPermission() && remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
#else
        yield break;
#endif
    }

    private static bool HasLocationPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(FineLocationPermission) ||
               Permission.HasUserAuthorizedPermission(CoarseLocationPermission);
#else
        return true;
#endif
    }

    private PhoneLocationSample BuildSample(LocationInfo location)
    {
        var sample = new PhoneLocationSample
        {
            DeviceId = DeviceId,
            Latitude = location.latitude,
            Longitude = location.longitude,
            AltitudeMeters = location.altitude,
            HorizontalAccuracyMeters = location.horizontalAccuracy,
            VerticalAccuracyMeters = location.verticalAccuracy,
            Timestamp = location.timestamp,
            Sequence = ++_sequence,
            SpeedMetersPerSecond = -1f,
            CourseDegrees = -1f
        };

        if (_hasLocation && _previousSample.IsValid)
        {
            FillMovementFields(ref sample, _previousSample);
        }

        return sample;
    }

    private static void FillMovementFields(ref PhoneLocationSample sample, PhoneLocationSample previous)
    {
        var deltaTime = sample.Timestamp - previous.Timestamp;
        if (deltaTime <= 0.001d)
        {
            return;
        }

        var offset = GetMetersOffset(previous.Latitude, previous.Longitude, sample.Latitude, sample.Longitude);
        var distance = offset.magnitude;
        sample.SpeedMetersPerSecond = distance / (float)deltaTime;

        if (distance > 0.05f)
        {
            var course = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            sample.CourseDegrees = course < 0f ? course + 360f : course;
        }
    }

    private static Vector2 GetMetersOffset(double originLatitude, double originLongitude, double latitude, double longitude)
    {
        const double earthRadiusMeters = 6378137d;

        var originLatitudeRadians = originLatitude * Mathf.Deg2Rad;
        var latitudeRadians = latitude * Mathf.Deg2Rad;
        var deltaLatitude = (latitude - originLatitude) * Mathf.Deg2Rad;
        var deltaLongitude = (longitude - originLongitude) * Mathf.Deg2Rad;
        var averageLatitude = (originLatitudeRadians + latitudeRadians) * 0.5d;

        var eastMeters = deltaLongitude * Math.Cos(averageLatitude) * earthRadiusMeters;
        var northMeters = deltaLatitude * earthRadiusMeters;
        return new Vector2((float)eastMeters, (float)northMeters);
    }

    private void Fail(string error)
    {
        _startRoutine = null;
        Failed?.Invoke(error);
        Debug.LogWarning(error, this);
    }
}
