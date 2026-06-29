using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YandexHelmetNavigator : MonoBehaviour, IYandexHelmetNavigator
{
    [Header("Dependencies")]
    [Tooltip("Клиент, который запрашивает маршруты через Yandex Route API.")]
    [SerializeField] private YandexMapsRouteClient routeClient;

    [Tooltip("OpenRouteService client. When assigned, it is used instead of YandexMapsRouteClient.")]
    [SerializeField] private OpenRouteServiceClient openRouteServiceClient;

    [Tooltip("Presenter, который рисует полученный маршрут в пространстве шлема.")]
    [SerializeField] private YandexHelmetRoutePresenter routePresenter;

    [Header("Default Route")]
    [Tooltip("Необязательная стартовая координата для маршрута по умолчанию и инициализации CurrentCoordinate.")]
    [SerializeField] private GeoCoordinate startCoordinate;

    [Tooltip("Необязательная конечная координата маршрута по умолчанию, который можно показать на Start.")]
    [SerializeField] private GeoCoordinate finishCoordinate;

    [Tooltip("Копирует Start Coordinate в CurrentCoordinate во время Awake, если Start Coordinate валидна.")]
    [SerializeField] private bool useStartCoordinateAsCurrent = true;

    [Tooltip("Запрашивает и рисует маршрут по умолчанию в Start.")]
    [SerializeField] private bool showDefaultRouteOnStart;

    [Header("Route Completion")]
    [SerializeField] private bool completeAtSceneEndpoint = true;
    [SerializeField, Min(0.1f)] private float sceneEndpointDistanceMeters = 0.5f;

    private Coroutine _activeRequest;
    private MonoBehaviour _activeRequestOwner;
    private int _requestVersion;
    private bool _isRequesting;
    private GeoCoordinate? _currentCoordinate;
    private GeoCoordinate? _currentDestination;

    public event Action<YandexRouteResult> RouteRequestCompleted;
    public event Action RouteDisabled;

    public GeoCoordinate? CurrentCoordinate => _currentCoordinate;
    public GeoCoordinate? CurrentDestination => _currentDestination;
    public bool IsRouteVisible => routePresenter != null && routePresenter.IsVisible;
    public bool IsRequesting => _isRequesting;
    public bool HasGeographicNorthAlignment => routePresenter != null && routePresenter.HasGeographicNorthAlignment;
    public float GeographicNorthYawDegrees => routePresenter != null ? routePresenter.GeographicNorthYawDegrees : 0f;
    public Transform AnchorTransform => routePresenter != null ? routePresenter.Anchor : transform;
    private IYandexRouteClient ActiveRouteClient => openRouteServiceClient != null ? openRouteServiceClient : routeClient;
    private YandexRouteTravelMode ActiveDefaultMode => openRouteServiceClient != null
        ? openRouteServiceClient.DefaultMode
        : routeClient != null ? routeClient.DefaultMode : YandexRouteTravelMode.Walking;

    private void Awake()
    {
        if (routeClient == null)
        {
            routeClient = GetComponent<YandexMapsRouteClient>();
        }

        if (openRouteServiceClient == null)
        {
            openRouteServiceClient = GetComponent<OpenRouteServiceClient>();
        }

        if (routePresenter == null)
        {
            routePresenter = GetComponent<YandexHelmetRoutePresenter>();
        }

        if (useStartCoordinateAsCurrent && startCoordinate.IsValid)
        {
            _currentCoordinate = startCoordinate;
        }
    }

    private void Start()
    {
        if (showDefaultRouteOnStart && startCoordinate.IsValid && finishCoordinate.IsValid)
        {
            ShowRoute(startCoordinate, finishCoordinate);
        }
    }

    private void Update()
    {
        if (completeAtSceneEndpoint &&
            routePresenter != null &&
            routePresenter.IsPlayerWithinEndpointDistance(sceneEndpointDistanceMeters))
        {
            DisableRoute();
        }
    }

    public void SetDependencies(YandexMapsRouteClient client, YandexHelmetRoutePresenter presenter)
    {
        routeClient = client;
        openRouteServiceClient = null;
        routePresenter = presenter;
    }

    public void SetDependencies(OpenRouteServiceClient client, YandexHelmetRoutePresenter presenter)
    {
        openRouteServiceClient = client;
        routePresenter = presenter;
    }

    public void SetCurrentCoordinate(GeoCoordinate coordinate)
    {
        _currentCoordinate = coordinate;
    }

    public void SetCurrentCoordinate(double latitude, double longitude)
    {
        SetCurrentCoordinate(new GeoCoordinate(latitude, longitude));
    }

    public bool TryAlignGeographicNorthToCourse(float courseDegrees)
    {
        return routePresenter != null && routePresenter.TryAlignGeographicNorthToCourse(courseDegrees);
    }

    public bool TryAlignGeographicNorthToHeading(float headingDegrees)
    {
        return routePresenter != null && routePresenter.TryAlignGeographicNorthToHeading(headingDegrees);
    }

    public bool SetGeographicNorthYaw(float yawDegrees)
    {
        if (routePresenter == null)
        {
            return false;
        }

        routePresenter.SetGeographicNorthYaw(yawDegrees);
        return true;
    }

    public Coroutine ShowRoute(double startLatitude, double startLongitude, double finishLatitude, double finishLongitude)
    {
        return ShowRoute(
            new GeoCoordinate(startLatitude, startLongitude),
            new GeoCoordinate(finishLatitude, finishLongitude));
    }

    public Coroutine ShowRoute(GeoCoordinate start, GeoCoordinate finish, Action<YandexRouteResult> completed = null)
    {
        _currentCoordinate = start;
        _currentDestination = finish;
        return RequestAndShowRoute(new YandexRouteRequest(start, finish, ActiveDefaultMode), completed);
    }

    public Coroutine ShowRouteTo(GeoCoordinate finish, Action<YandexRouteResult> completed = null)
    {
        if (!_currentCoordinate.HasValue)
        {
            CompleteWithFailure("Current coordinate is not set.", completed);
            return null;
        }

        return ShowRoute(_currentCoordinate.Value, finish, completed);
    }

    public Coroutine BuildRouteTo(GeoCoordinate finish, Action<YandexRouteResult> completed = null)
    {
        return ShowRouteTo(finish, completed);
    }

    public bool TryRebuildRouteFromCurrentCoordinate(Action<YandexRouteResult> completed = null)
    {
        if (!_currentDestination.HasValue)
        {
            return false;
        }

        ShowRouteTo(_currentDestination.Value, completed);
        return true;
    }

    public Coroutine RequestAndShowRoute(YandexRouteRequest request, Action<YandexRouteResult> completed = null)
    {
        var activeRouteClient = ActiveRouteClient;
        if (activeRouteClient == null)
        {
            CompleteWithFailure("Route client is not assigned.", completed);
            return null;
        }

        if (routePresenter == null)
        {
            CompleteWithFailure("Yandex route presenter is not assigned.", completed);
            return null;
        }

        var keepVisibleRouteOnFailure = IsRouteVisible;
        CancelActiveRequest();
        var requestVersion = ++_requestVersion;
        _isRequesting = true;
        _activeRequestOwner = activeRouteClient as MonoBehaviour;

        var requestCoroutine = activeRouteClient.RequestRoute(request, result =>
        {
            if (requestVersion != _requestVersion)
            {
                return;
            }

            _activeRequest = null;
            _activeRequestOwner = null;
            _isRequesting = false;

            if (result.Succeeded)
            {
                if (result.Route?.Points != null &&
                    result.Route.Points.Count > 0 &&
                    result.Route.Points[0].IsValid)
                {
                    routePresenter.SetGeographicOrigin(result.Route.Points[0]);
                }

                routePresenter.TryShowRoute(result.Route);
            }
            else if (!keepVisibleRouteOnFailure)
            {
                routePresenter.DisableRoute();
            }

            RouteRequestCompleted?.Invoke(result);
            completed?.Invoke(result);
        });

        // StartCoroutine can invoke the callback synchronously before returning when
        // validation fails, so do not resurrect an already completed request here.
        if (_isRequesting && requestVersion == _requestVersion)
        {
            _activeRequest = requestCoroutine;
        }

        return _activeRequest;
    }

    public void Disable()
    {
        DisableRoute();
    }

    public void DisableRoute()
    {
        CancelActiveRequest();

        if (routePresenter != null)
        {
            routePresenter.DisableRoute();
        }

        _currentDestination = null;
        RouteDisabled?.Invoke();
    }

    private void CancelActiveRequest()
    {
        if (_activeRequest == null && !_isRequesting)
        {
            return;
        }

        _requestVersion++;
        if (_activeRequest != null && _activeRequestOwner != null)
        {
            _activeRequestOwner.StopCoroutine(_activeRequest);
        }

        _activeRequest = null;
        _activeRequestOwner = null;
        _isRequesting = false;
    }

    private void CompleteWithFailure(string error, Action<YandexRouteResult> completed)
    {
        var result = YandexRouteResult.Failure(error);
        RouteRequestCompleted?.Invoke(result);
        completed?.Invoke(result);
    }
}
