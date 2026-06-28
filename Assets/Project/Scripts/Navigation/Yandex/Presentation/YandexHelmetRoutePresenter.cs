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
    [SerializeField] private bool followPlayerTransform;

    [Tooltip("Поворачивает маршрут вместе с трансформом игрока.")]
    [SerializeField] private bool rotateWithPlayer;

    [Tooltip("Использует только поворот игрока по Y, чтобы линия оставалась горизонтальной.")]
    [SerializeField] private bool yawOnlyRotation = true;

    [Tooltip("Поворот географического севера относительно мирового +Z. Не зависит от текущего поворота головы.")]
    [SerializeField] private float geographicNorthYawDegrees;

    [Header("Route Projection")]
    [Tooltip("Масштаб перевода реальных метров маршрута в Unity units. При стандартном масштабе Unity 1 unit = 1 meter.")]
    [SerializeField, Min(0.001f)] private float metersToUnityScale = 1f;

    [Tooltip("Максимальное количество точек маршрута, передаваемых в отображение.")]
    [SerializeField, Min(2)] private int maxRenderedPoints = 768;

    [Tooltip("Если исходных точек больше Max Rendered Points, рисует только начало маршрута вместо упрощения всей линии до финиша.")]
    [SerializeField] private bool drawRoutePrefixWhenPointBudgetExceeded = true;

    [Tooltip("Допустимое отклонение упрощенной линии от исходного маршрута в метрах.")]
    [SerializeField, Min(0.01f)] private float simplificationToleranceMeters = 0.1f;

    [Header("Rendering")]
    [Tooltip("Компонент, который отображает рассчитанные точки маршрута.")]
    [SerializeField] private RoutePathView pathView;

    [Tooltip("Разрешает RoutePathView применять свой Point Projector. Для Yandex-маршрута обычно выключено, чтобы линия оставалась на заданной высоте, а не ложилась на MRMesh.")]
    [SerializeField] private bool usePathViewPointProjector;

    [Tooltip("Разрешает RoutePathView применять свой Point Offset. Для Yandex-маршрута обычно выключено, чтобы общий offset NavMesh-линии не поднимал маршрут к голове.")]
    [SerializeField] private bool usePathViewPointOffset;

    [Tooltip("Смещение тени относительно уже отрисованной линии Yandex-маршрута.")]
    [SerializeField] private Vector3 shadowOffsetRelativeToRoute = new Vector3(0f, -0.02f, 0f);

    private readonly List<Vector3> _localRoutePoints = new List<Vector3>();
    private readonly List<Vector3> _sourceLocalRoutePoints = new List<Vector3>();
    private readonly List<Vector3> _visibleSourceLocalRoutePoints = new List<Vector3>();
    private readonly List<Vector3> _worldRoutePoints = new List<Vector3>();
    private YandexRouteData _currentRoute;
    private bool _hasGeographicNorthAlignment;
    private bool _isVisibleRouteTruncated;
    private bool _hasWorldRouteEndpoint;
    private Vector3 _localRouteEndpoint;
    private Vector3 _worldRouteEndpoint;
    private GeoCoordinate? _geographicOrigin;

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
    public float GeographicNorthYawDegrees => geographicNorthYawDegrees;
    public bool HasGeographicNorthAlignment => _hasGeographicNorthAlignment;
    public Transform Anchor => AnchorTransform;

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
        _visibleSourceLocalRoutePoints.Clear();
        _worldRoutePoints.Clear();
        _isVisibleRouteTruncated = false;
        _hasWorldRouteEndpoint = false;
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

    public void SetGeographicOrigin(GeoCoordinate origin)
    {
        _geographicOrigin = origin.IsValid ? origin : (GeoCoordinate?)null;
    }

    public bool IsPlayerWithinEndpointDistance(float distanceMeters)
    {
        if (!IsVisible || playerTransform == null || !_hasWorldRouteEndpoint)
        {
            return false;
        }

        var delta = playerTransform.position - _worldRouteEndpoint;
        delta.y = 0f;
        return delta.sqrMagnitude <= distanceMeters * distanceMeters;
    }

    public bool TryAlignGeographicNorthToCourse(float courseDegrees)
    {
        if (playerTransform == null || courseDegrees < 0f || courseDegrees > 360f)
        {
            return false;
        }

        if (_hasGeographicNorthAlignment)
        {
            return true;
        }

        return TryAlignGeographicNorthToHeading(courseDegrees);
    }

    public bool TryAlignGeographicNorthToHeading(float headingDegrees)
    {
        if (playerTransform == null || headingDegrees < 0f || headingDegrees >= 360f)
        {
            return false;
        }

        geographicNorthYawDegrees = Mathf.DeltaAngle(0f, playerTransform.eulerAngles.y - headingDegrees);
        _hasGeographicNorthAlignment = true;
        RebuildView();
        return true;
    }

    public Quaternion GetGeographicNorthRotation()
    {
        return Quaternion.Euler(0f, geographicNorthYawDegrees, 0f);
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
        _sourceLocalRoutePoints.Clear();
        _visibleSourceLocalRoutePoints.Clear();
        _isVisibleRouteTruncated = false;
        _hasWorldRouteEndpoint = false;

        if (routePoints == null || routePoints.Count < 2)
        {
            pathView?.Hide();
            return false;
        }

        var origin = _geographicOrigin ?? routePoints[0];
        for (var i = 0; i < routePoints.Count; i++)
        {
            AddProjectedPoint(_sourceLocalRoutePoints, origin, routePoints[i]);
        }

        _localRouteEndpoint = _sourceLocalRoutePoints[_sourceLocalRoutePoints.Count - 1];

        var sourceForRendering = (IReadOnlyList<Vector3>)_sourceLocalRoutePoints;
        if (drawRoutePrefixWhenPointBudgetExceeded && _sourceLocalRoutePoints.Count > maxRenderedPoints)
        {
            var pointCount = Mathf.Clamp(maxRenderedPoints, 2, _sourceLocalRoutePoints.Count);
            _visibleSourceLocalRoutePoints.Clear();
            for (var i = 0; i < pointCount; i++)
            {
                _visibleSourceLocalRoutePoints.Add(_sourceLocalRoutePoints[i]);
            }

            _isVisibleRouteTruncated = true;
            sourceForRendering = _visibleSourceLocalRoutePoints;
        }

        var tolerance = Mathf.Max(0.01f, simplificationToleranceMeters) * metersToUnityScale;
        SimplifyRoute(sourceForRendering, tolerance, _localRoutePoints);

        // Increase tolerance only when necessary to respect the renderer budget for a full route.
        for (var attempt = 0;
             !_isVisibleRouteTruncated && _localRoutePoints.Count > maxRenderedPoints && attempt < 24;
             attempt++)
        {
            tolerance *= 1.5f;
            SimplifyRoute(_sourceLocalRoutePoints, tolerance, _localRoutePoints);
        }

        return _localRoutePoints.Count >= 2;
    }

    private void AddProjectedPoint(List<Vector3> target, GeoCoordinate origin, GeoCoordinate point)
    {
        var metersOffset = GeoCoordinateUtility.GetMetersOffset(origin, point);
        target.Add(new Vector3(
            metersOffset.x * metersToUnityScale,
            0f,
            metersOffset.y * metersToUnityScale));
    }

    private static void SimplifyRoute(IReadOnlyList<Vector3> source, float tolerance, List<Vector3> target)
    {
        target.Clear();
        if (source == null || source.Count == 0)
        {
            return;
        }

        if (source.Count <= 2)
        {
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }

            return;
        }

        var keep = new bool[source.Count];
        keep[0] = true;
        keep[source.Count - 1] = true;
        var ranges = new Stack<Vector2Int>();
        ranges.Push(new Vector2Int(0, source.Count - 1));
        var toleranceSquared = tolerance * tolerance;

        while (ranges.Count > 0)
        {
            var range = ranges.Pop();
            var furthestIndex = -1;
            var furthestDistanceSquared = 0f;

            for (var i = range.x + 1; i < range.y; i++)
            {
                var distanceSquared = GetSegmentDistanceSquared(source[i], source[range.x], source[range.y]);
                if (distanceSquared > furthestDistanceSquared)
                {
                    furthestDistanceSquared = distanceSquared;
                    furthestIndex = i;
                }
            }

            if (furthestIndex < 0 || furthestDistanceSquared <= toleranceSquared)
            {
                continue;
            }

            keep[furthestIndex] = true;
            ranges.Push(new Vector2Int(range.x, furthestIndex));
            ranges.Push(new Vector2Int(furthestIndex, range.y));
        }

        for (var i = 0; i < source.Count; i++)
        {
            if (keep[i])
            {
                target.Add(source[i]);
            }
        }
    }

    private static float GetSegmentDistanceSquared(Vector3 point, Vector3 start, Vector3 end)
    {
        var segment = end - start;
        segment.y = 0f;
        var fromStart = point - start;
        fromStart.y = 0f;
        var segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= 0.000001f)
        {
            return fromStart.sqrMagnitude;
        }

        var t = Mathf.Clamp01(Vector3.Dot(fromStart, segment) / segmentLengthSquared);
        var closest = start + segment * t;
        var delta = point - closest;
        delta.y = 0f;
        return delta.sqrMagnitude;
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

        _worldRouteEndpoint = origin + rotation * _localRouteEndpoint;
        _hasWorldRouteEndpoint = true;

        return pathView.ShowWithRelativeShadow(
            _worldRoutePoints,
            usePathViewPointProjector,
            usePathViewPointOffset,
            shadowOffsetRelativeToRoute,
            !_isVisibleRouteTruncated);
    }

    private Vector3 GetRouteOrigin(Transform anchor, Quaternion rotation)
    {
        if (!rotateWithPlayer)
        {
            var playerYaw = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
            return anchor.position + playerYaw * routeOffset;
        }

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
            return Quaternion.Euler(0f, geographicNorthYawDegrees, 0f);
        }

        if (!yawOnlyRotation)
        {
            return anchor.rotation;
        }

        return Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
    }
}
