using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneLocationYandexBuildingProbeBridge : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhoneLocationYandexNavigatorBridge locationBridge;
    [SerializeField] private YandexHelmetNavigator navigator;
    [SerializeField] private SpatialMeshYandexBuildingProbe buildingProbe;
    [SerializeField] private Transform geoAnchorTransform;

    [Header("Behavior")]
    [SerializeField] private bool applyLatestOnEnable = true;
    [SerializeField] private bool requireGeographicNorthAlignment = true;

    private void Awake()
    {
        if (locationBridge == null)
        {
            locationBridge = GetComponent<PhoneLocationYandexNavigatorBridge>();
        }

        if (navigator == null)
        {
            navigator = GetComponent<YandexHelmetNavigator>();
        }

        if (buildingProbe == null)
        {
            buildingProbe = FindObjectOfType<SpatialMeshYandexBuildingProbe>();
        }
    }

    private void OnEnable()
    {
        if (locationBridge != null)
        {
            locationBridge.LocationApplied += Apply;

            if (applyLatestOnEnable)
            {
                locationBridge.ApplyLatestLocation();
            }
        }
    }

    private void OnDisable()
    {
        if (locationBridge != null)
        {
            locationBridge.LocationApplied -= Apply;
        }
    }

    public void Apply(PhoneLocationSample sample)
    {
        if (buildingProbe == null || !sample.IsValid)
        {
            return;
        }

        if (navigator != null &&
            requireGeographicNorthAlignment &&
            !navigator.HasGeographicNorthAlignment &&
            locationBridge != null)
        {
            locationBridge.TryAlignGeographicNorthFromLatestHeading(true);
        }

        if (navigator != null &&
            requireGeographicNorthAlignment &&
            !navigator.HasGeographicNorthAlignment)
        {
            return;
        }

        var anchor = geoAnchorTransform != null
            ? geoAnchorTransform
            : navigator != null ? navigator.AnchorTransform : transform;

        buildingProbe.SetGeoAnchor(anchor, new GeoCoordinate(sample.Latitude, sample.Longitude));

        if (navigator != null && navigator.HasGeographicNorthAlignment)
        {
            buildingProbe.SetGeographicNorthYaw(navigator.GeographicNorthYawDegrees);
        }
        
    }
}
