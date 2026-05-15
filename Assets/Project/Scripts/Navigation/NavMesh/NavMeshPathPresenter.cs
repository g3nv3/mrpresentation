using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class NavMeshPathPresenter : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private Transform pathStart;
    [SerializeField] private Vector3 floorOffset = new Vector3(0f, 0.03f, 0f);
    [SerializeField] private float navMeshSampleRadius = 1.5f;
    [SerializeField] private int areaMask = NavMesh.AllAreas;
    [SerializeField] private bool showPartialPaths;

    [Header("Floor Raycast")]
    [SerializeField] private bool useFloorRaycast = true;
    [SerializeField] private LayerMask floorRaycastMask = ~0;
    [SerializeField] private float floorRaycastStartHeight = 1.5f;
    [SerializeField] private float floorRaycastDistance = 3f;
    [SerializeField, Range(0f, 90f)] private float maxFloorAngle = 45f;

    [Header("Refresh")]
    [SerializeField] private bool refreshWhileActive = true;
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Rendering")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private bool hideOnStart = true;

    private readonly NavMeshPath _path = new NavMeshPath();

    private Transform _currentTarget;
    private float _nextRefreshTime;

    public Transform CurrentTarget => _currentTarget;
    public bool IsVisible => lineRenderer != null && lineRenderer.enabled && _currentTarget != null;
    public bool IsActive => _currentTarget != null;

    private Transform PathStart => pathStart != null ? pathStart : transform;

    private void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;

        if (hideOnStart)
        {
            DisablePath();
        }
    }

    private void Update()
    {
        if (!refreshWhileActive || !IsActive || Time.time < _nextRefreshTime)
        {
            return;
        }

        RebuildCurrentPath();
    }

    public void BuildPathTo(Transform target)
    {
        TryShowPathTo(target);
    }

    public void ShowPathTo(Transform target)
    {
        TryShowPathTo(target);
    }

    public bool TryShowPathTo(Transform target)
    {
        if (target == null)
        {
            DisablePath();
            return false;
        }

        _currentTarget = target;
        return RebuildCurrentPath();
    }

    public void TogglePathTo(Transform target)
    {
        if (IsActive && _currentTarget == target)
        {
            DisablePath();
            return;
        }

        TryShowPathTo(target);
    }

    public void Toggle(Transform target)
    {
        TogglePathTo(target);
    }

    public void Disable()
    {
        DisablePath();
    }

    public void DisablePath()
    {
        _currentTarget = null;

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }

    public bool RebuildCurrentPath()
    {
        _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);

        if (_currentTarget == null || lineRenderer == null)
        {
            DisablePath();
            return false;
        }

        if (!TryGetFloorPosition(PathStart.position, out var startFloorPosition) ||
            !TryGetFloorPosition(_currentTarget.position, out var endFloorPosition) ||
            !TryGetNavMeshPosition(startFloorPosition, out var startPosition) ||
            !TryGetNavMeshPosition(endFloorPosition, out var endPosition))
        {
            HideRenderer();
            return false;
        }

        if (!NavMesh.CalculatePath(startPosition, endPosition, areaMask, _path))
        {
            HideRenderer();
            return false;
        }

        if (_path.status == NavMeshPathStatus.PathInvalid ||
            (_path.status == NavMeshPathStatus.PathPartial && !showPartialPaths))
        {
            HideRenderer();
            return false;
        }

        DrawPath(_path.corners);
        return lineRenderer.enabled;
    }

    public void SetPathStart(Transform start)
    {
        pathStart = start;

        if (IsActive)
        {
            RebuildCurrentPath();
        }
    }

    private bool TryGetNavMeshPosition(Vector3 sourcePosition, out Vector3 navMeshPosition)
    {
        if (NavMesh.SamplePosition(sourcePosition, out var hit, navMeshSampleRadius, areaMask))
        {
            navMeshPosition = hit.position;
            return true;
        }

        navMeshPosition = default;
        return false;
    }

    private void DrawPath(Vector3[] corners)
    {
        if (corners == null || corners.Length < 2)
        {
            HideRenderer();
            return;
        }

        lineRenderer.positionCount = corners.Length;

        for (var i = 0; i < corners.Length; i++)
        {
            var point = TryGetFloorPosition(corners[i], out var floorPosition)
                ? floorPosition
                : corners[i];

            lineRenderer.SetPosition(i, point + floorOffset);
        }

        lineRenderer.enabled = true;
    }

    private bool TryGetFloorPosition(Vector3 sourcePosition, out Vector3 floorPosition)
    {
        if (!useFloorRaycast)
        {
            floorPosition = sourcePosition;
            return true;
        }

        var rayOrigin = sourcePosition + Vector3.up * floorRaycastStartHeight;
        var rayDistance = floorRaycastStartHeight + floorRaycastDistance;

        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayDistance, floorRaycastMask, QueryTriggerInteraction.Ignore) &&
            IsFloorNormal(hit.normal))
        {
            floorPosition = hit.point;
            return true;
        }

        floorPosition = default;
        return false;
    }

    private bool IsFloorNormal(Vector3 normal)
    {
        var minDot = Mathf.Cos(maxFloorAngle * Mathf.Deg2Rad);
        return Vector3.Dot(normal.normalized, Vector3.up) >= minDot;
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
