using System.Net;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YandexFollowingRouteMiniMap : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhoneLocationUdpReceiver locationReceiver;
    [SerializeField] private PhoneRouteDestinationYandexNavigatorBridge routeBridge;
    [SerializeField] private YandexHelmetNavigator navigator;
    [SerializeField] private YandexMiniMapTileLayer tileLayer;
    [SerializeField] private YandexMiniMapRouteLine routeLine;

    [Header("UI")]
    [SerializeField] private GameObject mapViewRoot;
    [SerializeField] private CanvasGroup mapCanvasGroup;
    [SerializeField] private RectTransform mapContentRoot;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private RectTransform destinationMarker;

    [Header("View")]
    [SerializeField] private bool visibleOnEnable = true;
    [SerializeField, Range(1, 20)] private int zoom = 17;
    [SerializeField, Min(0.01f)] private float uiScale = 1f;
    [SerializeField] private bool rotateMapWithCourse;
    [SerializeField, Min(0f)] private float minimumCourseSpeedMetersPerSecond = 0.4f;
    [SerializeField, Range(0f, 30f)] private float headingSmoothing = 12f;

    private GeoCoordinate _center;
    private GeoCoordinate? _destination;
    private YandexRouteData _route;
    private bool _hasCenter;
    private bool _isMapVisible = true;
    private float _headingDegrees;
    private float _targetHeadingDegrees;

    public bool IsMapVisible => _isMapVisible;
    public bool HasCenter => _hasCenter;
    public bool HasRoute => _route != null;

    private void Awake()
    {
        if (locationReceiver == null)
        {
            locationReceiver = GetComponent<PhoneLocationUdpReceiver>();
        }

        if (routeBridge == null)
        {
            routeBridge = GetComponent<PhoneRouteDestinationYandexNavigatorBridge>();
        }

        if (navigator == null)
        {
            navigator = GetComponent<YandexHelmetNavigator>();
        }

        if (tileLayer == null)
        {
            tileLayer = GetComponentInChildren<YandexMiniMapTileLayer>(true);
        }

        if (routeLine == null)
        {
            routeLine = GetComponentInChildren<YandexMiniMapRouteLine>(true);
        }

        if (mapContentRoot == null && tileLayer != null)
        {
            mapContentRoot = tileLayer.transform as RectTransform;
        }
    }

    private void OnEnable()
    {
        SetMapVisible(visibleOnEnable);

        if (locationReceiver != null)
        {
            locationReceiver.LocationReceived += OnLocationReceived;
            if (locationReceiver.HasLocation)
            {
                ApplyLocation(locationReceiver.LatestSample);
            }
        }

        if (routeBridge != null)
        {
            routeBridge.RouteRequestCompleted += OnRouteRequestCompleted;
            routeBridge.RouteDisabled += OnRouteDisabled;
        }
        else if (navigator != null)
        {
            navigator.RouteRequestCompleted += OnRouteRequestCompleted;
            navigator.RouteDisabled += OnRouteDisabled;
        }

        if (!_hasCenter && navigator != null && navigator.CurrentCoordinate.HasValue)
        {
            SetCenter(navigator.CurrentCoordinate.Value);
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (locationReceiver != null)
        {
            locationReceiver.LocationReceived -= OnLocationReceived;
        }

        if (routeBridge != null)
        {
            routeBridge.RouteRequestCompleted -= OnRouteRequestCompleted;
            routeBridge.RouteDisabled -= OnRouteDisabled;
        }
        else if (navigator != null)
        {
            navigator.RouteRequestCompleted -= OnRouteRequestCompleted;
            navigator.RouteDisabled -= OnRouteDisabled;
        }
    }

    private void Update()
    {
        if (rotateMapWithCourse)
        {
            var t = headingSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-headingSmoothing * Time.unscaledDeltaTime);
            _headingDegrees = Mathf.LerpAngle(_headingDegrees, _targetHeadingDegrees, t);
            ApplyMapRotation();
        }

        if (_hasCenter)
        {
            UpdateDestinationMarker();
        }
    }

    public void SetApiKey(string apiKey)
    {
        if (tileLayer != null)
        {
            tileLayer.SetApiKey(apiKey);
        }
    }

    public void ShowMap()
    {
        SetMapVisible(true);
    }

    public void HideMap()
    {
        SetMapVisible(false);
    }

    public void ToggleMap()
    {
        SetMapVisible(!_isMapVisible);
    }

    public void SetMapVisible(bool visible)
    {
        _isMapVisible = visible;

        if (mapCanvasGroup != null)
        {
            mapCanvasGroup.alpha = visible ? 1f : 0f;
            mapCanvasGroup.interactable = visible;
            mapCanvasGroup.blocksRaycasts = visible;
        }
        else if (mapViewRoot != null)
        {
            mapViewRoot.SetActive(visible);
        }

        if (visible)
        {
            RefreshAll();
        }
    }

    public void SetCenter(GeoCoordinate center)
    {
        if (!center.IsValid)
        {
            _hasCenter = false;
            tileLayer?.Clear();
            routeLine?.Clear();
            SetMarkerVisible(destinationMarker, false);
            return;
        }

        _center = center;
        _hasCenter = true;
        RefreshAll();
    }

    public void SetRoute(YandexRouteData route)
    {
        _route = route;
        _destination = GetRouteDestination(route);

        if (routeLine != null)
        {
            routeLine.SetRoute(route);
        }

        RefreshRouteView();
        UpdateDestinationMarker();
    }

    public void ClearRoute()
    {
        _route = null;
        _destination = null;
        routeLine?.Clear();
        SetMarkerVisible(destinationMarker, false);
    }

    private void OnLocationReceived(PhoneLocationSample sample, IPEndPoint remoteEndPoint)
    {
        ApplyLocation(sample);
    }

    private void ApplyLocation(PhoneLocationSample sample)
    {
        if (!sample.IsValid)
        {
            return;
        }

        SetCenter(new GeoCoordinate(sample.Latitude, sample.Longitude));

        if (rotateMapWithCourse &&
            sample.CourseDegrees >= 0f &&
            sample.SpeedMetersPerSecond >= minimumCourseSpeedMetersPerSecond)
        {
            _targetHeadingDegrees = sample.CourseDegrees;
        }
    }

    private void OnRouteRequestCompleted(YandexRouteResult result)
    {
        if (result != null && result.Succeeded)
        {
            SetRoute(result.Route);
        }
        else
        {
            ClearRoute();
        }
    }

    private void OnRouteDisabled()
    {
        ClearRoute();
    }

    private void RefreshAll()
    {
        if (!_isMapVisible || !_hasCenter)
        {
            return;
        }

        if (playerMarker != null)
        {
            playerMarker.anchoredPosition = Vector2.zero;
        }

        if (tileLayer != null)
        {
            tileLayer.SetView(_center, zoom, uiScale);
        }

        RefreshRouteView();
        ApplyMapRotation();
        UpdateDestinationMarker();
    }

    private void RefreshRouteView()
    {
        if (!_isMapVisible || !_hasCenter || routeLine == null)
        {
            return;
        }

        if (_route != null)
        {
            routeLine.SetView(_center, zoom, uiScale);
        }
    }

    private void ApplyMapRotation()
    {
        if (!_isMapVisible || mapContentRoot == null)
        {
            return;
        }

        mapContentRoot.localEulerAngles = rotateMapWithCourse
            ? new Vector3(0f, 0f, _headingDegrees)
            : Vector3.zero;
    }

    private void UpdateDestinationMarker()
    {
        if (destinationMarker == null || !_hasCenter || !_destination.HasValue)
        {
            SetMarkerVisible(destinationMarker, false);
            return;
        }

        var centerPixel = YandexMiniMapProjection.GeoToWorldPixel(_center, zoom);
        var destinationPixel = YandexMiniMapProjection.GeoToWorldPixel(_destination.Value, zoom);
        destinationMarker.anchoredPosition =
            YandexMiniMapProjection.WorldPixelToUiOffset(destinationPixel, centerPixel, uiScale);
        SetMarkerVisible(destinationMarker, true);
    }

    private static GeoCoordinate? GetRouteDestination(YandexRouteData route)
    {
        if (route == null || route.Points == null || route.Points.Count == 0)
        {
            return null;
        }

        return route.Points[route.Points.Count - 1];
    }

    private static void SetMarkerVisible(RectTransform marker, bool visible)
    {
        if (marker != null && marker.gameObject.activeSelf != visible)
        {
            marker.gameObject.SetActive(visible);
        }
    }
}
