using System;
using Project.Scripts.UI;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneHelmetYawCalibration : MonoBehaviour, IUiToggleState
{
    [Header("Dependencies")]
    [SerializeField] private PhoneLocationUdpReceiver receiver;
    [SerializeField] private YandexHelmetNavigator navigator;
    [SerializeField] private SpatialMeshYandexBuildingProbe buildingProbe;
    [SerializeField] private Transform helmetTransform;
    [SerializeField] private Transform geoAnchorTransform;

    [Header("UI")]
    [SerializeField] private TMP_Text statusLabel;

    [Header("Guide Line")]
    [Tooltip("Optional line that shows the latest phone heading transformed into Unity space by the saved calibration.")]
    [SerializeField] private LineRenderer calibrationLine;
    [SerializeField, Min(0.1f)] private float calibrationLineLengthMeters = 2f;
    [SerializeField] private Vector3 calibrationLineOffset = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private bool showCalibrationLine = true;
    [SerializeField] private bool showOnlyWhenRouteVisible = true;

    [Header("Validation")]
    [SerializeField, Min(0f)] private float maximumHeadingAccuracyDegrees = 45f;

    private PhoneLocationSample _latestSample;
    private bool _hasLatestSample;
    private bool _hasCalibration;
    private float _geographicNorthYawDegrees;
    private float _calibratedPhoneHeadingDegrees;
    private float _calibratedHelmetYawDegrees;
    private string _lastStatusReason;

    public bool HasCalibration => _hasCalibration;
    public float GeographicNorthYawDegrees => _geographicNorthYawDegrees;
    public float CalibratedPhoneHeadingDegrees => _calibratedPhoneHeadingDegrees;
    public float CalibratedHelmetYawDegrees => _calibratedHelmetYawDegrees;
    public event Action<bool> CalibrationChanged;

    private Transform HelmetTransform
    {
        get
        {
            if (helmetTransform != null)
            {
                return helmetTransform;
            }

            return navigator != null && navigator.AnchorTransform != null
                ? navigator.AnchorTransform
                : transform;
        }
    }

    private void Awake()
    {
        ResolveDependencies();
        HideCalibrationLine();
        RefreshStatus();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
        RefreshStatus();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        RefreshCalibrationLine();
    }

    public bool CalibrateFromLatestHeading()
    {
        if (!_hasLatestSample && receiver != null && receiver.HasLocation)
        {
            SetLatestSample(receiver.LatestSample);
        }

        if (!_hasLatestSample)
        {
            SetStatusReason("Нет последнего GPS/heading пакета.");
            return false;
        }

        return Calibrate(_latestSample);
    }

    public bool Calibrate(PhoneLocationSample sample)
    {
        if (!TryGetReliableHeading(sample, out var headingDegrees, out var error))
        {
            SetStatusReason(error);
            return false;
        }

        var helmetYaw = HelmetTransform.eulerAngles.y;
        _geographicNorthYawDegrees = Mathf.DeltaAngle(0f, helmetYaw - headingDegrees);
        _calibratedPhoneHeadingDegrees = headingDegrees;
        _calibratedHelmetYawDegrees = helmetYaw;
        _hasCalibration = true;

        ApplyCalibrationToNavigator();
        ApplyCalibrationToBuildingProbe(sample);
        SetStatusReason(null);
        CalibrationChanged?.Invoke(true);
        RefreshStatus();
        RefreshCalibrationLine();
        return true;
    }

    public void ClearCalibration()
    {
        _hasCalibration = false;
        _geographicNorthYawDegrees = 0f;
        _calibratedPhoneHeadingDegrees = 0f;
        _calibratedHelmetYawDegrees = 0f;
        SetStatusReason("Калибровка сброшена.");
        HideCalibrationLine();
        CalibrationChanged?.Invoke(false);
        RefreshStatus();
    }

    public bool ApplyCalibrationToNavigator()
    {
        if (!_hasCalibration || navigator == null)
        {
            return false;
        }

        return navigator.SetGeographicNorthYaw(_geographicNorthYawDegrees);
    }

    public bool ApplyCalibrationToBuildingProbe()
    {
        if (!_hasLatestSample)
        {
            return false;
        }

        return ApplyCalibrationToBuildingProbe(_latestSample);
    }

    public bool ApplyCalibrationToBuildingProbe(PhoneLocationSample sample)
    {
        if (!_hasCalibration || buildingProbe == null || !sample.IsValid)
        {
            return false;
        }

        var anchor = geoAnchorTransform != null ? geoAnchorTransform : HelmetTransform;
        buildingProbe.SetGeoAnchor(anchor, new GeoCoordinate(sample.Latitude, sample.Longitude));
        buildingProbe.SetGeographicNorthYaw(_geographicNorthYawDegrees);
        return true;
    }

    public bool TryGetUnityYawForPhoneHeading(float phoneHeadingDegrees, out float unityYawDegrees)
    {
        unityYawDegrees = 0f;
        if (!_hasCalibration || phoneHeadingDegrees < 0f || phoneHeadingDegrees >= 360f)
        {
            return false;
        }

        unityYawDegrees = Mathf.Repeat(_geographicNorthYawDegrees + phoneHeadingDegrees, 360f);
        return true;
    }

    public void RefreshStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        if (!_hasCalibration)
        {
            statusLabel.text = string.IsNullOrWhiteSpace(_lastStatusReason)
                ? "Калибровка: нет"
                : "Калибровка: нет\n" + _lastStatusReason;
            return;
        }

        statusLabel.text = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Калибровка: есть\nNorth yaw: {0:F1}°\nPhone heading: {1:F1}°\nHelmet yaw: {2:F1}°",
            _geographicNorthYawDegrees,
            _calibratedPhoneHeadingDegrees,
            _calibratedHelmetYawDegrees);
    }

    private void ResolveDependencies()
    {
        if (receiver == null)
        {
            receiver = GetComponent<PhoneLocationUdpReceiver>();
        }

        if (navigator == null)
        {
            navigator = GetComponent<YandexHelmetNavigator>();
        }

        if (buildingProbe == null)
        {
            buildingProbe = FindFirstObjectByType<SpatialMeshYandexBuildingProbe>();
        }
    }

    private void Subscribe()
    {
        if (receiver != null)
        {
            receiver.LocationReceived += OnLocationReceived;
            if (receiver.HasLocation)
            {
                SetLatestSample(receiver.LatestSample);
            }
        }
    }

    private void Unsubscribe()
    {
        if (receiver != null)
        {
            receiver.LocationReceived -= OnLocationReceived;
        }
    }

    private void OnLocationReceived(PhoneLocationSample sample, System.Net.IPEndPoint remoteEndPoint)
    {
        SetLatestSample(sample);
        RefreshStatus();
    }

    public void HandleCalibrateButtonPressed()
    {
        CalibrateFromLatestHeading();
    }

    private void SetLatestSample(PhoneLocationSample sample)
    {
        _latestSample = sample;
        _hasLatestSample = sample.IsValid;
    }

    private bool TryGetReliableHeading(PhoneLocationSample sample, out float headingDegrees, out string error)
    {
        headingDegrees = 0f;
        error = null;

        if (!sample.IsValid)
        {
            error = "Последний пакет невалиден.";
            return false;
        }

        if (!sample.HasHeading || sample.HeadingDegrees < 0f || sample.HeadingDegrees >= 360f)
        {
            error = "В последнем пакете нет heading телефона.";
            return false;
        }

        if (sample.HeadingAccuracyDegrees >= 0f &&
            sample.HeadingAccuracyDegrees > maximumHeadingAccuracyDegrees)
        {
            error = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Heading неточный: {0:F1}° > {1:F1}°.",
                sample.HeadingAccuracyDegrees,
                maximumHeadingAccuracyDegrees);
            return false;
        }

        headingDegrees = sample.HeadingDegrees;
        return true;
    }

    private void RefreshCalibrationLine()
    {
        if (!showCalibrationLine || calibrationLine == null || !_hasCalibration || !_hasLatestSample)
        {
            HideCalibrationLine();
            return;
        }

        if (showOnlyWhenRouteVisible && (navigator == null || !navigator.IsRouteVisible))
        {
            HideCalibrationLine();
            return;
        }

        if (!_latestSample.HasHeading ||
            !TryGetUnityYawForPhoneHeading(_latestSample.HeadingDegrees, out var unityYaw))
        {
            HideCalibrationLine();
            return;
        }

        var anchor = HelmetTransform;
        var start = anchor.position + calibrationLineOffset;
        var direction = Quaternion.Euler(0f, unityYaw, 0f) * Vector3.forward;
        var end = start + direction * calibrationLineLengthMeters;

        calibrationLine.positionCount = 2;
        calibrationLine.SetPosition(0, start);
        calibrationLine.SetPosition(1, end);
        calibrationLine.enabled = true;
    }

    private void HideCalibrationLine()
    {
        if (calibrationLine == null)
        {
            return;
        }

        calibrationLine.positionCount = 0;
        calibrationLine.enabled = false;
    }

    private void SetStatusReason(string reason)
    {
        _lastStatusReason = reason;
        RefreshStatus();
    }

    public bool IsOn => showCalibrationLine;
    public event Action<bool> Changed;

    public void SetOn(bool value)
    {
        if (showCalibrationLine == value)
        {
            return;
        }

        showCalibrationLine = value;
        if (showCalibrationLine)
        {
            RefreshCalibrationLine();
        }
        else
        {
            HideCalibrationLine();
        }

        Changed?.Invoke(showCalibrationLine);
    }

    public void Toggle()
    {
        SetOn(!showCalibrationLine);
    }
}
