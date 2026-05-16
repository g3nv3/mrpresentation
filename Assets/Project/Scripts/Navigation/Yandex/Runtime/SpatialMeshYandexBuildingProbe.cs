using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpatialMeshYandexBuildingProbe : MonoBehaviour, ISpatialMeshYandexBuildingProbe
{
    [Header("Dependencies")]
    [Tooltip("Клиент, через который Yandex Geocoder проверяет объект в координате попадания raycast.")]
    [SerializeField] private YandexGeocoderClient geocoderClient;

    [Header("Spatial Mesh Raycast")]
    [Tooltip("Маска слоев для коллайдеров Pico spatial mesh. В этой сцене MRMesh находится на слое 3, поэтому маска равна 8.")]
    [SerializeField] private LayerMask spatialMeshMask = Physics.DefaultRaycastLayers;

    [Tooltip("Максимальная дистанция raycast от переданной точки origin.")]
    [SerializeField, Min(0.01f)] private float maxDistance = 30f;

    [Tooltip("Определяет, учитываются ли trigger-коллайдеры при raycast по spatial mesh.")]
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("Если включено, попаданием в spatial mesh считается только попадание в MeshCollider.")]
    [SerializeField] private bool requireMeshCollider = true;

    [Header("Geo Anchor")]
    [Tooltip("Трансформ в Unity, который соответствует Geo Anchor Coordinate. Обычно это XR Origin или другой откалиброванный якорь.")]
    [SerializeField] private Transform geoAnchorTransform;

    [Tooltip("Реальная координата точки Geo Anchor Transform. Ее нужно задать, чтобы hit point корректно переводился в широту и долготу.")]
    [SerializeField] private GeoCoordinate geoAnchorCoordinate;

    [Tooltip("Сколько Unity units соответствует одному реальному метру при переводе смещения hit point в широту и долготу.")]
    [SerializeField, Min(0.001f)] private float metersToUnityScale = 1f;

    [Tooltip("Использует поворот Geo Anchor Transform при переводе локального смещения hit point в восток/север.")]
    [SerializeField] private bool rotateWithAnchor = true;

    [Tooltip("Использует только поворот якоря по Y, игнорируя наклон вперед/назад и крен.")]
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
