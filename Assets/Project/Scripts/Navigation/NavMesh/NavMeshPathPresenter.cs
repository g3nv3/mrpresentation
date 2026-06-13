using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class NavMeshPathPresenter : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Трансформ начала пути. Если не задан, используется трансформ этого компонента.")]
    [SerializeField] private Transform pathStart;

    [Tooltip("Радиус поиска ближайшей точки NavMesh для начала и цели маршрута.")]
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    [Tooltip("Маска областей NavMesh, передаваемая в NavMesh.SamplePosition и NavMesh.CalculatePath.")]
    [SerializeField] private int areaMask = NavMesh.AllAreas;

    [Tooltip("Разрешает показывать частичный путь, если Unity не смогла построить полный маршрут.")]
    [SerializeField] private bool showPartialPaths;

    [Header("Projection")]
    [Tooltip("Компонент, реализующий IRoutePointProjector. Используется для привязки начала и цели к полу перед поиском NavMesh.")]
    [SerializeField] private RouteFloorProjector floorProjector;

    [Header("Refresh")]
    [Tooltip("Периодически перестраивает видимый путь, пока активна цель.")]
    [SerializeField] private bool refreshWhileActive = true;

    [Tooltip("Интервал в секундах между автоматическими перестроениями пути.")]
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Arrival")]
    [Tooltip("Дистанция до конечной точки, при которой маршрут считается завершенным.")]
    [SerializeField, Min(0f)] private float arrivalDistance = 0.35f;

    [Header("Rendering")]
    [Tooltip("Компонент, который отображает рассчитанные точки маршрута.")]
    [SerializeField] private RoutePathView pathView;

    [SerializeField] private bool verboseLogging;

    private NavMeshPath _path;
    private Transform _currentTarget;
    private float _nextRefreshTime;

    private IRoutePointProjector FloorProjector => floorProjector as IRoutePointProjector;

    public Transform CurrentTarget => _currentTarget;
    public bool IsVisible => pathView != null && pathView.IsVisible && _currentTarget != null;
    public bool IsActive => _currentTarget != null;

    private Transform PathStart => pathStart != null ? pathStart : transform;

    private void Awake()
    {
        _path = new NavMeshPath();
        EnsureDependencies();
        pathView?.Hide();
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
        TogglePathTo(target);
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
        pathView?.Hide();
    }

    public bool RebuildCurrentPath()
    {
        _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);

        _path ??= new NavMeshPath();
        EnsureDependencies();

        if (_currentTarget == null || pathView == null)
        {
            DisablePath();
            return false;
        }

        if (!TryGetProjectedPosition(PathStart.position, out var startFloorPosition))
        {
            LogWarning($"Path start is not above a valid floor. Source={PathStart.position}");
            HidePathView();
            return false;
        }

        if (!TryGetProjectedPosition(_currentTarget.position, out var endFloorPosition))
        {
            LogWarning($"Path target is not above a valid floor. Target={_currentTarget.name} Source={_currentTarget.position}");
            HidePathView();
            return false;
        }

        if (!TryGetNavMeshPosition(startFloorPosition, out var startPosition))
        {
            LogWarning($"Path start floor point is not near NavMesh. Floor={startFloorPosition} Radius={navMeshSampleRadius}. {GetNavMeshSummary()}");
            HidePathView();
            return false;
        }

        if (!TryGetNavMeshPosition(endFloorPosition, out var endPosition))
        {
            LogWarning($"Path target floor point is not near NavMesh. Target={_currentTarget.name} Floor={endFloorPosition} Radius={navMeshSampleRadius}. {GetNavMeshSummary()}");
            HidePathView();
            return false;
        }

        if (Vector3.Distance(startPosition, endPosition) <= arrivalDistance)
        {
            DisablePath();
            return false;
        }

        if (!NavMesh.CalculatePath(startPosition, endPosition, areaMask, _path))
        {
            LogWarning($"NavMesh.CalculatePath failed. Start={startPosition} End={endPosition}");
            HidePathView();
            return false;
        }

        if (_path.status == NavMeshPathStatus.PathInvalid)
        {
            LogWarning($"NavMesh path is invalid. Start={startPosition} End={endPosition}");
            HidePathView();
            return false;
        }

        if (_path.status == NavMeshPathStatus.PathPartial && !showPartialPaths)
        {
            LogWarning($"NavMesh path is partial and partial paths are disabled. Start={startPosition} End={endPosition}");
            HidePathView();
            return false;
        }

        return pathView.Show(_path.corners);
    }

    public void SetPathStart(Transform start)
    {
        pathStart = start;

        if (IsActive)
        {
            RebuildCurrentPath();
        }
    }

    private void EnsureDependencies()
    {
        if (pathView == null)
        {
            pathView = GetComponent<RoutePathView>();
        }

        if (floorProjector == null)
        {
            floorProjector = GetComponent<RouteFloorProjector>();
        }
    }

    private bool TryGetProjectedPosition(Vector3 sourcePosition, out Vector3 projectedPosition)
    {
        var projector = FloorProjector;
        if (projector == null)
        {
            projectedPosition = sourcePosition;
            return true;
        }

        return projector.TryProjectPoint(sourcePosition, out projectedPosition);
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

    private void HidePathView()
    {
        pathView?.Hide();
    }

    private void LogWarning(string message)
    {
        if (verboseLogging)
        {
            Debug.LogWarning(message, this);
        }
    }

    private void OnValidate()
    {
        if (floorProjector != null && !(floorProjector is IRoutePointProjector))
        {
            floorProjector = null;
        }
    }

    private static string GetNavMeshSummary()
    {
        var triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices == null || triangulation.vertices.Length == 0)
        {
            return "NavMesh triangulation is empty.";
        }

        var min = triangulation.vertices[0];
        var max = triangulation.vertices[0];
        for (var i = 1; i < triangulation.vertices.Length; i++)
        {
            var vertex = triangulation.vertices[i];
            min = Vector3.Min(min, vertex);
            max = Vector3.Max(max, vertex);
        }

        return $"NavMesh triangulation vertices={triangulation.vertices.Length}, indices={triangulation.indices.Length}, boundsMin={min}, boundsMax={max}.";
    }
}
