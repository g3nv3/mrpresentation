using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RoutePathView : MonoBehaviour
{
    [Tooltip("Основная линия маршрута.")]
    [SerializeField] private RouteLineRendererView pathLine;

    [Tooltip("Необязательная подложка/тень маршрута. Может использовать тот же проектор точки на пол.")]
    [SerializeField] private RouteLineRendererView floorShadowLine;
    [Tooltip("Необязательный маркер конечной точки.")]
    [SerializeField] private RouteEndpointMarkerView endpointMarker;

    [Header("Generated Floor Shadow")]
    [Tooltip("Автоматически создает дочерний LineRenderer тени, если Floor Shadow Line не назначен.")]
    [SerializeField] private bool autoCreateFloorShadow = true;

    [Tooltip("Материал автоматически созданной тени. Если не задан, используется материал основной линии.")]
    [SerializeField] private Material floorShadowMaterial;

    [Tooltip("Смещение автоматически созданной тени относительно точек маршрута.")]
    [SerializeField] private Vector3 floorShadowOffset = new Vector3(0f, 0.02f, 0f);

    [Tooltip("Множитель ширины автоматически созданной тени относительно основной линии.")]
    [SerializeField, Min(0f)] private float floorShadowWidthMultiplier = 1.35f;

    [Tooltip("Имя дочернего объекта, который будет создан для тени.")]
    [SerializeField] private string generatedFloorShadowName = "Route Floor Shadow";


    public bool IsVisible => pathLine != null && pathLine.IsVisible;

    private void Awake()
    {
        EnsureDependencies();
        Hide();
    }

    public bool Show(IReadOnlyList<Vector3> points)
    {
        EnsureDependencies();

        if (points == null || points.Count < 2)
        {
            Hide();
            return false;
        }

        if (pathLine == null || !pathLine.Show(points))
        {
            Hide();
            return false;
        }

        if (floorShadowLine != null)
        {
            floorShadowLine.Show(points);
        }

        if (endpointMarker != null)
        {
            endpointMarker.Show(points[points.Count - 1]);
        }

        return true;
    }

    public void Hide()
    {
        if (pathLine != null)
        {
            pathLine.Hide();
        }

        if (floorShadowLine != null)
        {
            floorShadowLine.Hide();
        }

        if (endpointMarker != null)
        {
            endpointMarker.Hide();
        }
    }

    private void EnsureDependencies()
    {
        if (pathLine == null)
        {
            pathLine = GetComponent<RouteLineRendererView>();
        }

        EnsureFloorShadowLine();
    }

    private void EnsureFloorShadowLine()
    {
        if (!autoCreateFloorShadow || floorShadowLine != null || pathLine == null)
        {
            return;
        }

        var shadowObject = new GameObject(string.IsNullOrWhiteSpace(generatedFloorShadowName)
            ? "Route Floor Shadow"
            : generatedFloorShadowName);

        shadowObject.transform.SetParent(transform, false);

        pathLine.EnsureRenderer();

        var shadowRenderer = shadowObject.AddComponent<LineRenderer>();
        var sourceRenderer = pathLine.Renderer;
        CopyLineRendererSettings(sourceRenderer, shadowRenderer);

        floorShadowLine = shadowObject.AddComponent<RouteLineRendererView>();
        floorShadowLine.SetPointProjector(pathLine.PointProjector);
        floorShadowLine.SetPointOffset(floorShadowOffset);
        floorShadowLine.Hide();
    }

    private void CopyLineRendererSettings(LineRenderer source, LineRenderer destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.useWorldSpace = true;
        destination.shadowCastingMode = ShadowCastingMode.Off;
        destination.receiveShadows = false;

        if (source == null)
        {
            destination.widthMultiplier = floorShadowWidthMultiplier;
            if (floorShadowMaterial != null)
            {
                destination.sharedMaterial = floorShadowMaterial;
            }

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
}
