using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YandexHelmetNavigator : MonoBehaviour, IYandexHelmetNavigator
{
    [Header("Dependencies")]
    [Tooltip("Клиент, который запрашивает маршруты через Yandex Route API.")]
    [SerializeField] private YandexMapsRouteClient routeClient;

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

    private Coroutine _activeRequest;
    private GeoCoordinate? _currentCoordinate;

    public event Action<YandexRouteResult> RouteRequestCompleted;
    public event Action RouteDisabled;

    public GeoCoordinate? CurrentCoordinate => _currentCoordinate;
    public bool IsRouteVisible => routePresenter != null && routePresenter.IsVisible;

    private void Awake()
    {
        if (routeClient == null)
        {
            routeClient = GetComponent<YandexMapsRouteClient>();
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

    public void SetDependencies(YandexMapsRouteClient client, YandexHelmetRoutePresenter presenter)
    {
        routeClient = client;
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

    public Coroutine ShowRoute(double startLatitude, double startLongitude, double finishLatitude, double finishLongitude)
    {
        return ShowRoute(
            new GeoCoordinate(startLatitude, startLongitude),
            new GeoCoordinate(finishLatitude, finishLongitude));
    }

    public Coroutine ShowRoute(GeoCoordinate start, GeoCoordinate finish, Action<YandexRouteResult> completed = null)
    {
        _currentCoordinate = start;
        return RequestAndShowRoute(new YandexRouteRequest(start, finish, routeClient != null ? routeClient.DefaultMode : YandexRouteTravelMode.Walking), completed);
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

    public Coroutine RequestAndShowRoute(YandexRouteRequest request, Action<YandexRouteResult> completed = null)
    {
        if (routeClient == null)
        {
            CompleteWithFailure("Yandex route client is not assigned.", completed);
            return null;
        }

        if (routePresenter == null)
        {
            CompleteWithFailure("Yandex route presenter is not assigned.", completed);
            return null;
        }

        CancelActiveRequest();
        _activeRequest = routeClient.RequestRoute(request, result =>
        {
            _activeRequest = null;

            if (result.Succeeded)
            {
                routePresenter.TryShowRoute(result.Route);
            }
            else
            {
                routePresenter.DisableRoute();
            }

            RouteRequestCompleted?.Invoke(result);
            completed?.Invoke(result);
        });

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

        RouteDisabled?.Invoke();
    }

    private void CancelActiveRequest()
    {
        if (_activeRequest == null)
        {
            return;
        }

        StopCoroutine(_activeRequest);
        _activeRequest = null;
    }

    private void CompleteWithFailure(string error, Action<YandexRouteResult> completed)
    {
        var result = YandexRouteResult.Failure(error);
        RouteRequestCompleted?.Invoke(result);
        completed?.Invoke(result);
    }
}
