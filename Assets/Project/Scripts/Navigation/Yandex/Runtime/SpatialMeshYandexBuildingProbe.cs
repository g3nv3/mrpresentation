using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpatialMeshYandexBuildingProbe : MonoBehaviour, ISpatialMeshYandexBuildingProbe
{
    [Header("Dependencies")]
    [Tooltip("Client used to ask Yandex Geocoder what object exists at the raycast hit coordinate.")]
    [SerializeField] private YandexGeocoderClient geocoderClient;

    [Header("Spatial Mesh Raycast")]
    [Tooltip("Layer mask for Pico spatial mesh colliders. In this scene MRMesh is layer 3, so the mask is 8.")]
    [SerializeField] private LayerMask spatialMeshMask = Physics.DefaultRaycastLayers;

    [Tooltip("Maximum raycast distance from the supplied origin.")]
    [SerializeField, Min(0.01f)] private float maxDistance = 30f;

    [Tooltip("Controls whether trigger colliders are considered by the spatial mesh raycast.")]
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("When enabled, only hits on MeshCollider are accepted as spatial mesh hits.")]
    [SerializeField] private bool requireMeshCollider = true;

    [Header("Geo Anchor")]
    [Tooltip("World transform that represents geoAnchorCoordinate in Unity space. Usually XR Origin or another calibrated world anchor.")]
    [SerializeField] private Transform geoAnchorTransform;

    [Tooltip("Real-world coordinate at geoAnchorTransform. Must be set before probe results can be geocoded correctly.")]
    [SerializeField] private GeoCoordinate geoAnchorCoordinate;

    [Tooltip("How many Unity units represent one real meter for converting hit offsets to latitude/longitude.")]
    [SerializeField, Min(0.001f)] private float metersToUnityScale = 1f;

    [Tooltip("Uses geoAnchorTransform rotation when converting local hit offsets to east/north meters.")]
    [SerializeField] private bool rotateWithAnchor = true;

    [Tooltip("Uses only anchor yaw for geo conversion, ignoring pitch and roll.")]
    [SerializeField] private bool yawOnlyRotation = true;

    private void Awake()
    {
        if (geocoderClient == null)
        {
            geocoderClient = GetComponent<YandexGeocoderClient>();
        }

        if (geoAnchorTransform == null)
        {
            geoAnchorTransform = transform;
        }
    }

    public void SetDependencies(YandexGeocoderClient client)
    {
        geocoderClient = client;
    }

    public void SetGeoAnchor(Transform anchorTransform, GeoCoordinate anchorCoordinate)
    {
        geoAnchorTransform = anchorTransform;
        geoAnchorCoordinate = anchorCoordinate;
    }

    public Coroutine Probe(Vector3 origin, Vector3 direction, Action<SpatialMeshYandexProbeResult> completed)
    {
        return StartCoroutine(ProbeRoutine(origin, direction, completed));
    }

    public IEnumerator ProbeRoutine(Vector3 origin, Vector3 direction, Action<SpatialMeshYandexProbeResult> completed)
    {
        if (!TryRaycastSpatialMesh(origin, direction, out var hit))
        {
            completed?.Invoke(SpatialMeshYandexProbeResult.NoHit());
            yield break;
        }

        GeoCoordinate coordinate;
        try
        {
            coordinate = WorldToGeoCoordinate(hit.point);
        }
        catch (Exception exception)
        {
            completed?.Invoke(SpatialMeshYandexProbeResult.Failure(hit, default, exception.Message));
            yield break;
        }

        if (geocoderClient == null)
        {
            completed?.Invoke(SpatialMeshYandexProbeResult.Failure(hit, coordinate, "Yandex Geocoder client is not assigned."));
            yield break;
        }

        YandexReverseGeocodeResult geocodeResult = null;
        yield return geocoderClient.ReverseGeocodeRoutine(coordinate, result => geocodeResult = result);
        completed?.Invoke(SpatialMeshYandexProbeResult.Success(hit, coordinate, geocodeResult));
    }

    public bool TryRaycastSpatialMesh(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        hit = default;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        if (!Physics.Raycast(origin, direction.normalized, out hit, maxDistance, spatialMeshMask, triggerInteraction))
        {
            return false;
        }

        return !requireMeshCollider || hit.collider is MeshCollider;
    }

    public GeoCoordinate WorldToGeoCoordinate(Vector3 worldPosition)
    {
        if (!geoAnchorCoordinate.IsValid)
        {
            throw new InvalidOperationException("Geo anchor coordinate is invalid.");
        }

        var anchor = geoAnchorTransform != null ? geoAnchorTransform : transform;
        var rotation = GetGeoRotation(anchor);
        var localDelta = Quaternion.Inverse(rotation) * (worldPosition - anchor.position);
        var metersOffset = new Vector2(
            localDelta.x / metersToUnityScale,
            localDelta.z / metersToUnityScale);

        return GeoCoordinateUtility.GetCoordinateFromMetersOffset(geoAnchorCoordinate, metersOffset);
    }

    private Quaternion GetGeoRotation(Transform anchor)
    {
        if (!rotateWithAnchor)
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
