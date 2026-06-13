using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class RouteLineRendererView : MonoBehaviour
{
    [Tooltip("LineRenderer, которым рисуется маршрут.")]
    [SerializeField] private LineRenderer lineRenderer;

    [Tooltip("Компонент, реализующий IRoutePointProjector. Если не задан, точки рисуются как есть.")]
    [SerializeField] private MonoBehaviour pointProjector;

    [Tooltip("Смещение в мировых координатах, добавляемое к каждой точке линии.")]
    [SerializeField] private Vector3 pointOffset;

    [Tooltip("Очищает и выключает линию при Awake.")]
    [SerializeField] private bool hideOnStart = true;

    private IRoutePointProjector Projector => pointProjector as IRoutePointProjector;

    public LineRenderer Renderer => lineRenderer;
    public MonoBehaviour PointProjector => pointProjector;
    public Vector3 PointOffset => pointOffset;
    public bool IsVisible => lineRenderer != null && lineRenderer.enabled && lineRenderer.positionCount > 1;

    private void Awake()
    {
        EnsureRenderer();

        if (hideOnStart)
        {
            Hide();
        }
    }

    public bool Show(IReadOnlyList<Vector3> points)
    {
        EnsureRenderer();

        if (lineRenderer == null || points == null || points.Count < 2)
        {
            Hide();
            return false;
        }

        lineRenderer.positionCount = points.Count;
        for (var i = 0; i < points.Count; i++)
        {
            lineRenderer.SetPosition(i, GetRenderPoint(points[i]));
        }

        lineRenderer.enabled = true;
        return true;
    }

    public void Hide()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }

    public void SetPointProjector(MonoBehaviour projector)
    {
        pointProjector = projector;
    }

    public void SetPointOffset(Vector3 offset)
    {
        pointOffset = offset;
    }

    private Vector3 GetRenderPoint(Vector3 sourcePoint)
    {
        var projector = Projector;
        if (projector != null && projector.TryProjectPoint(sourcePoint, out var projectedPoint))
        {
            return projectedPoint + pointOffset;
        }

        return sourcePoint + pointOffset;
    }

    public void EnsureRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
        }
    }

    private void OnValidate()
    {
        if (pointProjector != null && !(pointProjector is IRoutePointProjector))
        {
            pointProjector = null;
        }
    }
}
