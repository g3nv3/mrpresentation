using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class YandexHelmetRoutePresenter : MonoBehaviour, IYandexRoutePresenter
{
    [Header("Player Anchor")]
    [Tooltip("Transform used as the route origin in the headset. If empty, this component transform is used.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Local offset from the player transform where the route line starts.")]
    [SerializeField] private Vector3 routeOffset = new Vector3(0f, -0.35f, 1.2f);

    [Tooltip("Rebuilds line positions every LateUpdate so the route follows the player.")]
    [SerializeField] private bool followPlayerTransform = true;

    [Tooltip("Rotates the route with the player transform.")]
    [SerializeField] private bool rotateWithPlayer = true;

    [Tooltip("Uses only player yaw for route rotation, keeping the line level.")]
    [SerializeField] private bool yawOnlyRotation = true;

    [Header("Route Projection")]
    [Tooltip("Scale from real route meters to Unity units for headset visualization.")]
    [SerializeField, Min(0.001f)] private float metersToUnityScale = 0.03f;

    [Tooltip("Maximum number of route points sent to LineRenderer.")]
    [SerializeField, Min(2)] private int maxRenderedPoints = 256;

    [Header("Rendering")]
    [Tooltip("LineRenderer used to draw the Yandex route.")]
    [SerializeField] private LineRenderer lineRenderer;

    [Tooltip("Clears and disables the line on Awake.")]
    [SerializeField] private bool hideOnStart = true;

    private readonly List<Vector3> _localRoutePoints = new List<Vector3>();
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
    public bool IsVisible => lineRenderer != null && lineRenderer.enabled && lineRenderer.positionCount > 1;

    private Transform AnchorTransform => playerTransform != null ? playerTransform : transform;

    private void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;

        if (hideOnStart)
        {
            DisableRoute();
        }
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
        HideRenderer();
    }

    public void RebuildView()
    {
        if (_localRoutePoints.Count < 2)
        {
            HideRenderer();
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

    private bool TryBuildLocalRoute(IReadOnlyList<GeoCoordinate> routePoints)
    {
        _localRoutePoints.Clear();

        if (routePoints == null || routePoints.Count < 2)
        {
            HideRenderer();
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
        if (lineRenderer == null || _localRoutePoints.Count < 2)
        {
            HideRenderer();
            return false;
        }

        var anchor = AnchorTransform;
        var rotation = GetRouteRotation(anchor);
        var origin = GetRouteOrigin(anchor, rotation);

        lineRenderer.positionCount = _localRoutePoints.Count;
        for (var i = 0; i < _localRoutePoints.Count; i++)
        {
            lineRenderer.SetPosition(i, origin + rotation * _localRoutePoints[i]);
        }

        lineRenderer.enabled = true;
        return true;
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

    private void HideRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }
}
