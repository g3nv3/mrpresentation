using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YandexMiniMapRouteLine : MaskableGraphic
{
    [SerializeField, Min(1f)] private float lineWidth = 7f;
    [SerializeField, Min(2)] private int maxRenderedPoints = 384;

    private readonly List<GeoCoordinate> _routePoints = new List<GeoCoordinate>();
    private readonly List<Vector2> _uiPoints = new List<Vector2>();
    private GeoCoordinate _center;
    private int _zoom = 17;
    private float _uiScale = 1f;
    private bool _hasCenter;

    public bool HasRoute => _routePoints.Count >= 2;

    public void SetRoute(YandexRouteData route)
    {
        _routePoints.Clear();

        if (route != null && route.Points != null)
        {
            var step = Mathf.Max(1, Mathf.CeilToInt(route.Points.Count / (float)maxRenderedPoints));
            for (var i = 0; i < route.Points.Count; i += step)
            {
                _routePoints.Add(route.Points[i]);
            }

            if (route.Points.Count > 0)
            {
                var last = route.Points[route.Points.Count - 1];
                var addedLast = _routePoints.Count > 0 ? _routePoints[_routePoints.Count - 1] : default(GeoCoordinate);
                if (_routePoints.Count == 0 ||
                    Mathf.Abs((float)(last.Latitude - addedLast.Latitude)) > 0.0000001f ||
                    Mathf.Abs((float)(last.Longitude - addedLast.Longitude)) > 0.0000001f)
                {
                    _routePoints.Add(last);
                }
            }
        }

        RebuildUiPoints();
    }

    public void SetView(GeoCoordinate center, int zoom, float uiScale)
    {
        if (!center.IsValid)
        {
            _hasCenter = false;
            _uiPoints.Clear();
            SetVerticesDirty();
            return;
        }

        _center = center;
        _zoom = Mathf.Clamp(zoom, 1, 20);
        _uiScale = Mathf.Max(0.01f, uiScale);
        _hasCenter = true;
        RebuildUiPoints();
    }

    public void Clear()
    {
        _routePoints.Clear();
        _uiPoints.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_uiPoints.Count < 2)
        {
            return;
        }

        for (var i = 0; i < _uiPoints.Count - 1; i++)
        {
            AddLineSegment(vh, _uiPoints[i], _uiPoints[i + 1], lineWidth, color);
        }
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        RebuildUiPoints();
    }

    private void RebuildUiPoints()
    {
        _uiPoints.Clear();

        if (!_hasCenter || _routePoints.Count < 2)
        {
            SetVerticesDirty();
            return;
        }

        var centerPixel = YandexMiniMapProjection.GeoToWorldPixel(_center, _zoom);
        var rect = rectTransform.rect;

        for (var i = 0; i < _routePoints.Count; i++)
        {
            var pointPixel = YandexMiniMapProjection.GeoToWorldPixel(_routePoints[i], _zoom);
            _uiPoints.Add(rect.center + YandexMiniMapProjection.WorldPixelToUiOffset(pointPixel, centerPixel, _uiScale));
        }

        SetVerticesDirty();
    }

    private static void AddLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color segmentColor)
    {
        var delta = b - a;
        if (delta.sqrMagnitude < 0.001f)
        {
            return;
        }

        var direction = delta.normalized;
        var normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        var startIndex = vh.currentVertCount;

        vh.AddVert(a - normal, segmentColor, Vector2.zero);
        vh.AddVert(a + normal, segmentColor, Vector2.zero);
        vh.AddVert(b + normal, segmentColor, Vector2.zero);
        vh.AddVert(b - normal, segmentColor, Vector2.zero);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }
}
