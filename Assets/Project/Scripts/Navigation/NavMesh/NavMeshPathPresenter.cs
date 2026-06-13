using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class NavMeshPathPresenter : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Трансформ начала пути. Если не задан, используется трансформ этого компонента.")]
    [SerializeField] private Transform pathStart;

    [Tooltip("Смещение в мировых координатах, добавляемое к каждой точке линии. Обычно это небольшой подъем над полом.")]
    [SerializeField] private Vector3 floorOffset = new Vector3(0f, 0.03f, 0f);

    [Tooltip("Радиус поиска ближайшей точки NavMesh для начала и цели маршрута.")]
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    [Tooltip("Маска областей NavMesh, передаваемая в NavMesh.SamplePosition и NavMesh.CalculatePath.")]
    [SerializeField] private int areaMask = NavMesh.AllAreas;

    [Tooltip("Разрешает показывать частичный путь, если Unity не смогла построить полный маршрут.")]
    [SerializeField] private bool showPartialPaths;

    [Header("Floor Raycast")]
    [Tooltip("Проецирует точки отображаемого пути на коллайдеры пола перед отрисовкой.")]
    [SerializeField] private bool useFloorRaycast = true;

    [Tooltip("Маска слоев для raycast-проекции на пол.")]
    [SerializeField] private LayerMask floorRaycastMask = ~0;

    [Tooltip("Высота над исходной точкой, откуда начинается raycast вниз для поиска пола.")]
    [SerializeField] private float floorRaycastStartHeight = 1.5f;

    [Tooltip("Дополнительная дистанция ниже исходной точки, проверяемая raycast-ом пола.")]
    [SerializeField] private float floorRaycastDistance = 3f;

    [Tooltip("Максимальный угол между нормалью поверхности и направлением вверх, при котором поверхность считается полом.")]
    [SerializeField, Range(0f, 90f)] private float maxFloorAngle = 45f;

    [Header("Refresh")]
    [Tooltip("Периодически перестраивает видимый путь, пока активна цель.")]
    [SerializeField] private bool refreshWhileActive = true;

    [Tooltip("Интервал в секундах между автоматическими перестроениями пути.")]
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Arrival")]
    [Tooltip("Дистанция до конечной точки, при которой маршрут считается завершенным.")]
    [SerializeField, Min(0f)] private float arrivalDistance = 0.35f;

    [Header("Rendering")]
    [Tooltip("LineRenderer, которым рисуется путь по NavMesh.")]
    [SerializeField] private LineRenderer lineRenderer;

    [Tooltip("Очищает и выключает линию при Awake.")]
    [SerializeField] private bool hideOnStart = true;

    [Header("Floor Shadow")]
    [Tooltip("Включает дешевую подложку маршрута на полу вместо реального Shadow Casting.")]
    [SerializeField] private bool useFloorShadow = true;

    [Tooltip("LineRenderer подложки маршрута. Если не задан, будет создан дочерний LineRenderer.")]
    [SerializeField] private LineRenderer floorShadowLineRenderer;

    [Tooltip("Материал подложки маршрута. Если не задан, будет использован материал основной линии.")]
    [SerializeField] private Material floorShadowMaterial;

    [Tooltip("Отдельное смещение подложки относительно спроецированных на пол точек.")]
    [SerializeField] private Vector3 floorShadowOffset = new Vector3(0f, 0.02f, 0f);

    [Tooltip("Множитель ширины подложки относительно основной линии.")]
    [SerializeField, Min(0f)] private float floorShadowWidthMultiplier = 1.35f;

    [Header("Endpoint Marker")]
    [Tooltip("GameObject маркера конечной точки. Объект будет переноситься в endpoint и включаться/выключаться вместе с маршрутом.")]
    [SerializeField] private GameObject endpointMarkerObject;

    [Tooltip("SpriteRenderer маркера конечной точки. Используется для обратной совместимости, если Endpoint Marker Object не задан.")]
    [SerializeField] private SpriteRenderer endpointMarkerRenderer;

    [Tooltip("Спрайт маркера конечной точки, используется при автоматическом создании объекта, если Endpoint Marker Object не задан.")]
    [SerializeField] private Sprite endpointSprite;

    [Tooltip("Смещение маркера конечной точки относительно пола.")]
    [SerializeField] private Vector3 endpointMarkerOffset = new Vector3(0f, 0.08f, 0f);

    [Tooltip("Масштаб автоматически созданного маркера конечной точки.")]
    [SerializeField] private Vector3 endpointMarkerScale = Vector3.one * 0.25f;

    [SerializeField] private bool verboseLogging;

    private NavMeshPath _path;

    private Transform _currentTarget;
    private float _nextRefreshTime;

    public Transform CurrentTarget => _currentTarget;
    public bool IsVisible => lineRenderer != null && lineRenderer.enabled && _currentTarget != null;
    public bool IsActive => _currentTarget != null;

    private Transform PathStart => pathStart != null ? pathStart : transform;

    private void Awake()
    {
        _path = new NavMeshPath();

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        EnsureFloorShadowRenderer();
        EnsureEndpointMarker();

        if (hideOnStart)
        {
            DisablePath();
            return;
        }

        HideEndpointMarker();
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

        if (lineRenderer == null)
        {
            HideFloorShadowRenderer();
            HideEndpointMarker();
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
        HideFloorShadowRenderer();
        HideEndpointMarker();
    }

    public bool RebuildCurrentPath()
    {
        _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);

        _path ??= new NavMeshPath();

        if (_currentTarget == null || lineRenderer == null)
        {
            DisablePath();
            return false;
        }

        if (!TryGetFloorPosition(PathStart.position, out var startFloorPosition))
        {
            LogWarning($"Path start is not above a valid floor. Source={PathStart.position}");
            HideRenderer();
            return false;
        }

        if (!TryGetFloorPosition(_currentTarget.position, out var endFloorPosition))
        {
            LogWarning($"Path target is not above a valid floor. Target={_currentTarget.name} Source={_currentTarget.position}");
            HideRenderer();
            return false;
        }

        if (!TryGetNavMeshPosition(startFloorPosition, out var startPosition))
        {
            LogWarning($"Path start floor point is not near NavMesh. Floor={startFloorPosition} Radius={navMeshSampleRadius}. {GetNavMeshSummary()}");
            HideRenderer();
            return false;
        }

        if (!TryGetNavMeshPosition(endFloorPosition, out var endPosition))
        {
            LogWarning($"Path target floor point is not near NavMesh. Target={_currentTarget.name} Floor={endFloorPosition} Radius={navMeshSampleRadius}. {GetNavMeshSummary()}");
            HideRenderer();
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
            HideRenderer();
            return false;
        }

        if (_path.status == NavMeshPathStatus.PathInvalid)
        {
            LogWarning($"NavMesh path is invalid. Start={startPosition} End={endPosition}");
            HideRenderer();
            return false;
        }

        if (_path.status == NavMeshPathStatus.PathPartial && !showPartialPaths)
        {
            LogWarning($"NavMesh path is partial and partial paths are disabled. Start={startPosition} End={endPosition}");
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
            DisablePath();
            return;
        }

        DrawLine(lineRenderer, corners, floorOffset);

        lineRenderer.enabled = true;
        DrawFloorShadow(corners);
        ShowEndpointMarker(corners[corners.Length - 1]);
    }

    private void DrawLine(LineRenderer renderer, Vector3[] corners, Vector3 offset)
    {
        renderer.positionCount = corners.Length;

        for (var i = 0; i < corners.Length; i++)
        {
            var point = TryGetFloorPosition(corners[i], out var floorPosition)
                ? floorPosition
                : corners[i];

            renderer.SetPosition(i, point + offset);
        }
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
            HideFloorShadowRenderer();
            HideEndpointMarker();
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
        HideFloorShadowRenderer();
        HideEndpointMarker();
    }

    private void EnsureFloorShadowRenderer()
    {
        if (!useFloorShadow)
        {
            return;
        }

        if (floorShadowLineRenderer == null)
        {
            var shadowObject = new GameObject("NavMesh Path Floor Shadow");
            shadowObject.transform.SetParent(transform, false);
            floorShadowLineRenderer = shadowObject.AddComponent<LineRenderer>();
            CopyLineRendererSettings(lineRenderer, floorShadowLineRenderer);
        }

        floorShadowLineRenderer.useWorldSpace = true;
        floorShadowLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        floorShadowLineRenderer.receiveShadows = false;
        floorShadowLineRenderer.widthMultiplier = lineRenderer.widthMultiplier * floorShadowWidthMultiplier;

        if (floorShadowMaterial != null)
        {
            floorShadowLineRenderer.sharedMaterial = floorShadowMaterial;
        }

        HideFloorShadowRenderer();
    }

    private void CopyLineRendererSettings(LineRenderer source, LineRenderer destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.widthMultiplier = source.widthMultiplier * floorShadowWidthMultiplier;
        destination.widthCurve = source.widthCurve;
        destination.colorGradient = source.colorGradient;
        destination.numCornerVertices = source.numCornerVertices;
        destination.numCapVertices = source.numCapVertices;
        destination.alignment = source.alignment;
        destination.textureMode = source.textureMode;
        destination.textureScale = source.textureScale;
        destination.sharedMaterial = floorShadowMaterial != null ? floorShadowMaterial : source.sharedMaterial;
    }

    private void DrawFloorShadow(Vector3[] corners)
    {
        if (!useFloorShadow)
        {
            HideFloorShadowRenderer();
            return;
        }

        EnsureFloorShadowRenderer();

        if (floorShadowLineRenderer == null)
        {
            return;
        }

        DrawLine(floorShadowLineRenderer, corners, floorShadowOffset);
        floorShadowLineRenderer.enabled = true;
    }

    private void HideFloorShadowRenderer()
    {
        if (floorShadowLineRenderer == null)
        {
            return;
        }

        floorShadowLineRenderer.positionCount = 0;
        floorShadowLineRenderer.enabled = false;
    }

    private void EnsureEndpointMarker()
    {
        if (endpointMarkerObject != null)
        {
            if (endpointMarkerRenderer == null)
            {
                endpointMarkerRenderer = endpointMarkerObject.GetComponentInChildren<SpriteRenderer>(true);
            }

            return;
        }

        if (endpointMarkerRenderer != null)
        {
            endpointMarkerObject = endpointMarkerRenderer.gameObject;
            return;
        }

        if (endpointSprite == null)
        {
            return;
        }

        var markerObject = new GameObject("NavMesh Endpoint Marker");
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localScale = endpointMarkerScale;
        endpointMarkerObject = markerObject;
        endpointMarkerRenderer = markerObject.AddComponent<SpriteRenderer>();
        endpointMarkerRenderer.sprite = endpointSprite;
        endpointMarkerObject.SetActive(false);
    }

    private void ShowEndpointMarker(Vector3 endpointPosition)
    {
        EnsureEndpointMarker();

        if (endpointMarkerObject == null && endpointMarkerRenderer == null)
        {
            return;
        }

        var point = TryGetFloorPosition(endpointPosition, out var floorPosition)
            ? floorPosition
            : endpointPosition;

        var markerTransform = endpointMarkerObject != null
            ? endpointMarkerObject.transform
            : endpointMarkerRenderer.transform;

        markerTransform.position = point + endpointMarkerOffset;

        if (endpointMarkerObject != null)
        {
            endpointMarkerObject.SetActive(true);
        }
        else
        {
            endpointMarkerRenderer.enabled = true;
        }
    }

    private void HideEndpointMarker()
    {
        if (endpointMarkerObject != null)
        {
            endpointMarkerObject.SetActive(false);
            return;
        }

        if (endpointMarkerRenderer != null)
        {
            endpointMarkerRenderer.enabled = false;
        }
    }

    private void LogWarning(string message)
    {
        if (verboseLogging)
        {
            Debug.LogWarning(message, this);
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
