using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public sealed class QrPoseResolverDebugView : IQrPoseResolverView
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool parentRuntimeRootToHost;
    [SerializeField] private Transform centerMarker;
    [SerializeField] private Transform resultPointMarkerTemplate;
    [SerializeField, Min(0.005f)] private float runtimeCenterMarkerScale = 0.03f;
    [SerializeField, Min(0.005f)] private float runtimeResultPointMarkerScale = 0.02f;
    [SerializeField] private Color runtimeCenterMarkerColor = new Color(0.2f, 0.95f, 0.35f, 0.85f);
    [SerializeField] private Color runtimeResultPointMarkerColor = new Color(1f, 0.82f, 0.18f, 0.85f);

    [NonSerialized] private Transform hostTransform;
    [NonSerialized] private Transform runtimeRoot;
    [NonSerialized] private Transform runtimeCenterMarker;
    [NonSerialized] private List<Transform> runtimeResultPointMarkers;

    public bool IsEnabled => isEnabled;

    public void Initialize(Transform host)
    {
        hostTransform = host;
        SanitizeMarker(centerMarker);
        SanitizeMarker(resultPointMarkerTemplate);
    }

    public void Show(Pose? resolvedPose, IReadOnlyList<Vector3> resultPointPositions)
    {
        if (!isEnabled)
        {
            return;
        }

        EnsureRuntimeState();
        if (resolvedPose.HasValue)
        {
            UpdateMarker(GetCenterMarker(), resolvedPose.Value.position, true);
        }
        else
        {
            SetMarkerVisible(centerMarker, false);
            SetMarkerVisible(runtimeCenterMarker, false);
        }

        var visibleResultPointCount = resultPointPositions?.Count ?? 0;
        EnsureResultPointMarkers(visibleResultPointCount);

        for (var i = 0; i < visibleResultPointCount; i++)
        {
            UpdateMarker(runtimeResultPointMarkers[i], resultPointPositions[i], true);
        }

        HideResultPointMarkers(visibleResultPointCount);
    }

    public void Hide()
    {
        SetMarkerVisible(centerMarker, false);
        SetMarkerVisible(runtimeCenterMarker, false);
        HideResultPointMarkers(0);
    }

    private void EnsureRuntimeState()
    {
        runtimeResultPointMarkers ??= new List<Transform>();
    }

    private Transform GetCenterMarker()
    {
        if (runtimeCenterMarker != null)
        {
            return runtimeCenterMarker;
        }

        runtimeCenterMarker = CreateRuntimeMarker(
            "QR Debug Center",
            runtimeCenterMarkerScale,
            runtimeCenterMarkerColor);
        return runtimeCenterMarker;
    }

    private void EnsureResultPointMarkers(int requiredCount)
    {
        if (requiredCount <= 0)
        {
            return;
        }

        EnsureRuntimeState();

        while (runtimeResultPointMarkers.Count < requiredCount)
        {
            var marker = CreateResultPointMarker(runtimeResultPointMarkers.Count + 1);
            SetMarkerVisible(marker, false);
            runtimeResultPointMarkers.Add(marker);
        }
    }

    private void HideResultPointMarkers(int fromIndex)
    {
        if (runtimeResultPointMarkers == null)
        {
            return;
        }

        for (var i = fromIndex; i < runtimeResultPointMarkers.Count; i++)
        {
            SetMarkerVisible(runtimeResultPointMarkers[i], false);
        }
    }

    private Transform CreateResultPointMarker(int index)
    {
        return CreateRuntimeMarker(
            $"QR Debug Point {index}",
            runtimeResultPointMarkerScale,
            runtimeResultPointMarkerColor);
    }

    private Transform CreateRuntimeMarker(string markerName, float uniformScale, Color color)
    {
        EnsureRuntimeRoot();

        var markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = markerName;
        markerObject.transform.SetParent(runtimeRoot, false);
        markerObject.transform.localScale = Vector3.one * uniformScale;

        foreach (var collider in markerObject.GetComponentsInChildren<Collider>(true))
        {
            Object.Destroy(collider);
        }

        foreach (var renderer in markerObject.GetComponentsInChildren<Renderer>(true))
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader);
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }

                renderer.material = material;
            }
        }

        SanitizeMarker(markerObject.transform);
        return markerObject.transform;
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
        {
            return;
        }

        var rootObject = new GameObject("QR Pose Debug");
        runtimeRoot = rootObject.transform;
        if (parentRuntimeRootToHost && hostTransform != null)
        {
            runtimeRoot.SetParent(hostTransform, false);
        }
    }

    private static void SanitizeMarker(Transform marker)
    {
        if (marker == null)
        {
            return;
        }

        var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var childTransform in marker.GetComponentsInChildren<Transform>(true))
        {
            if (ignoreRaycastLayer >= 0)
            {
                childTransform.gameObject.layer = ignoreRaycastLayer;
            }
        }

        foreach (var collider in marker.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
    }

    private static void UpdateMarker(Transform marker, Vector3 position, bool isVisible)
    {
        if (marker == null)
        {
            return;
        }

        marker.position = position;
        SetMarkerVisible(marker, isVisible);
    }

    private static void SetMarkerVisible(Transform marker, bool isVisible)
    {
        if (marker == null || marker.gameObject.activeSelf == isVisible)
        {
            return;
        }

        marker.gameObject.SetActive(isVisible);
    }
}
