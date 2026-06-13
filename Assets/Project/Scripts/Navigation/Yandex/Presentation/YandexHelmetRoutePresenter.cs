using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YandexHelmetRoutePresenter : MonoBehaviour, IYandexRoutePresenter
{
    [Header("Player Anchor")]
    [Tooltip("Трансформ, относительно которого маршрут отображается в шлеме. Если не задан, используется трансформ этого компонента.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Локальное смещение от трансформа игрока, где начинается линия маршрута.")]
    [SerializeField] private Vector3 routeOffset = new Vector3(0f, -0.35f, 1.2f);

    [Tooltip("Пересчитывает точки линии каждый LateUpdate, чтобы маршрут следовал за игроком.")]
    [SerializeField] private bool followPlayerTransform = true;

    [Tooltip("Поворачивает маршрут вместе с трансформом игрока.")]
    [SerializeField] private bool rotateWithPlayer = true;

    [Tooltip("Использует только поворот игрока по Y, чтобы линия оставалась горизонтальной.")]
    [SerializeField] private bool yawOnlyRotation = true;

    [Header("Route Projection")]
    [Tooltip("Масштаб перевода реальных метров маршрута в Unity units для отображения в шлеме.")]
    [SerializeField, Min(0.001f)] private float metersToUnityScale = 0.03f;

    [Tooltip("Максимальное количество точек маршрута, передаваемых в отображение.")]
    [SerializeField, Min(2)] private int maxRenderedPoints = 256;

    [Header("Rendering")]
    [Tooltip("Компонент, который отображает рассчитанные точки маршрута.")]
    [SerializeField] private RoutePathView pathView;

    private readonly List<Vector3> _localRoutePoints = new List<Vector3>();
    private readonly List<Vector3> _worldRoutePoints = new List<Vector3>();
    private YandexRouteData _currentRoute;

    public Transform PlayerTransform
    {
        get => playerTransform;
        set
        {
            playerTransform = value;
            RebuildView();
        }
    }

    public Vector3 RouteOffset
    {
        get => routeOffset;
        set
        {
            routeOffset = value;
            RebuildView();
        }
    }

    public YandexRouteData CurrentRoute => _currentRoute;
    public bool IsVisible => pathView != null && pathView.IsVisible;

    private Transform AnchorTransform => playerTransform != null ? playerTransform : transform;

    private void Awake()
    {
        EnsureDependencies();
        pathView?.Hide();
    }

    private void LateUpdate()
    {
        if (followPlayerTransform && IsVisible)
        {
            RebuildView();
        }
    }

    public bool TryShowRoute(YandexRouteData route)
    {
        if (route == null)
        {
            DisableRoute();
            return false;
        }

        _currentRoute = route;
        return TryBuildLocalRoute(route.Points) && DrawLocalRoute();
    }

    public bool TryShowRoute(IReadOnlyList<GeoCoordinate> routePoints)
    {
        _currentRoute = null;
        return TryBuildLocalRoute(routePoints) && DrawLocalRoute();
    }

    public void ShowRoute(YandexRouteData route)
    {
        TryShowRoute(route);
    }

    public void ShowRoute(IReadOnlyList<GeoCoordinate> routePoints)
    {
        TryShowRoute(routePoints);
    }

    public void BuildPath(YandexRouteData route)
    {
        TryShowRoute(route);
    }

    public void Disable()
    {
        DisableRoute();
    }

    public void DisableRoute()
    {
        _currentRoute = null;
        _localRoutePoints.Clear();
        _worldRoutePoints.Clear();
        pathView?.Hide();
    }

    public void RebuildView()
    {
        if (_localRoutePoints.Count < 2)
        {
            pathView?.Hide();
            return;
        }

        DrawLocalRoute();
    }

    public void SetPlayerTransform(Transform target)
    {
        PlayerTransform = target;
    }

    public void SetRouteOffset(Vector3 offset)
    {
        RouteOffset = offset;
    }

    private void EnsureDependencies()
    {
        if (pathView == null)
        {
            pathView = GetComponent<RoutePathView>();
        }
    }

    private bool TryBuildLocalRoute(IReadOnlyList<GeoCoordinate> routePoints)
    {
        _localRoutePoints.Clear();

        if (routePoints == null || routePoints.Count < 2)
        {
            pathView?.Hide();
            return false;
        }

        var origin = routePoints[0];
        var step = Mathf.Max(1, Mathf.CeilToInt(routePoints.Count / (float)maxRenderedPoints));

        for (var i = 0; i < routePoints.Count; i += step)
        {
            AddProjectedPoint(origin, routePoints[i]);
        }

        if ((routePoints.Count - 1) % step != 0)
        {
            AddProjectedPoint(origin, routePoints[routePoints.Count - 1]);
        }

        return _localRoutePoints.Count >= 2;
    }

    private void AddProjectedPoint(GeoCoordinate origin, GeoCoordinate point)
    {
        var metersOffset = GeoCoordinateUtility.GetMetersOffset(origin, point);
        _localRoutePoints.Add(new Vector3(
            metersOffset.x * metersToUnityScale,
            0f,
            metersOffset.y * metersToUnityScale));
    }

    private bool DrawLocalRoute()
    {
        EnsureDependencies();

        if (pathView == null || _localRoutePoints.Count < 2)
        {
            pathView?.Hide();
            return false;
        }

        var anchor = AnchorTransform;
        var rotation = GetRouteRotation(anchor);
        var origin = GetRouteOrigin(anchor, rotation);

        _worldRoutePoints.Clear();
        for (var i = 0; i < _localRoutePoints.Count; i++)
        {
            _worldRoutePoints.Add(origin + rotation * _localRoutePoints[i]);
        }

        return pathView.Show(_worldRoutePoints);
    }

    private Vector3 GetRouteOrigin(Transform anchor, Quaternion rotation)
    {
        if (rotateWithPlayer && yawOnlyRotation)
        {
            return anchor.position + rotation * routeOffset;
        }

        return anchor.TransformPoint(routeOffset);
    }

    private Quaternion GetRouteRotation(Transform anchor)
    {
        if (!rotateWithPlayer)
        {
            return Quaternion.identity;
        }

        if (!yawOnlyRotation)
        {
            return anchor.rotation;
        }

        return Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
    }
}
