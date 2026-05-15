using System;
using System.Collections;
using UnityEngine;

public sealed class YandexReverseGeocodeResult
{
    public bool Succeeded { get; }
    public bool IsBuilding { get; }
    public GeoCoordinate Coordinate { get; }
    public string FullAddress { get; }
    public string ObjectName { get; }
    public string Description { get; }
    public string Kind { get; }
    public string Precision { get; }
    public string Error { get; }
    public string RawJson { get; }
    public long ResponseCode { get; }

    private YandexReverseGeocodeResult(
        bool succeeded,
        bool isBuilding,
        GeoCoordinate coordinate,
        string fullAddress,
        string objectName,
        string description,
        string kind,
        string precision,
        string error,
        string rawJson,
        long responseCode)
    {
        Succeeded = succeeded;
        IsBuilding = isBuilding;
        Coordinate = coordinate;
        FullAddress = fullAddress;
        ObjectName = objectName;
        Description = description;
        Kind = kind;
        Precision = precision;
        Error = error;
        RawJson = rawJson;
        ResponseCode = responseCode;
    }

    public static YandexReverseGeocodeResult Success(
        GeoCoordinate coordinate,
        bool isBuilding,
        string fullAddress,
        string objectName,
        string description,
        string kind,
        string precision,
        string rawJson,
        long responseCode)
    {
        return new YandexReverseGeocodeResult(
            true,
            isBuilding,
            coordinate,
            fullAddress,
            objectName,
            description,
            kind,
            precision,
            null,
            rawJson,
            responseCode);
    }

    public static YandexReverseGeocodeResult Failure(
        GeoCoordinate coordinate,
        string error,
        string rawJson = null,
        long responseCode = 0)
    {
        return new YandexReverseGeocodeResult(
            false,
            false,
            coordinate,
            null,
            null,
            null,
            null,
            null,
            error,
            rawJson,
            responseCode);
    }
}

public sealed class SpatialMeshYandexProbeResult
{
    public bool HasSpatialMeshHit { get; }
    public bool IsBuilding => GeocodeResult != null && GeocodeResult.IsBuilding;
    public bool IsPicoMeshOnly => HasSpatialMeshHit && GeocodeResult != null && GeocodeResult.Succeeded && !GeocodeResult.IsBuilding;
    public RaycastHit Hit { get; }
    public GeoCoordinate Coordinate { get; }
    public YandexReverseGeocodeResult GeocodeResult { get; }
    public string Error { get; }
    public string FullAddress => GeocodeResult != null ? GeocodeResult.FullAddress : null;

    private SpatialMeshYandexProbeResult(
        bool hasSpatialMeshHit,
        RaycastHit hit,
        GeoCoordinate coordinate,
        YandexReverseGeocodeResult geocodeResult,
        string error)
    {
        HasSpatialMeshHit = hasSpatialMeshHit;
        Hit = hit;
        Coordinate = coordinate;
        GeocodeResult = geocodeResult;
        Error = error;
    }

    public static SpatialMeshYandexProbeResult NoHit()
    {
        return new SpatialMeshYandexProbeResult(false, default, default, null, null);
    }

    public static SpatialMeshYandexProbeResult Failure(RaycastHit hit, GeoCoordinate coordinate, string error)
    {
        return new SpatialMeshYandexProbeResult(true, hit, coordinate, null, error);
    }

    public static SpatialMeshYandexProbeResult Success(
        RaycastHit hit,
        GeoCoordinate coordinate,
        YandexReverseGeocodeResult geocodeResult)
    {
        return new SpatialMeshYandexProbeResult(true, hit, coordinate, geocodeResult, geocodeResult != null ? geocodeResult.Error : null);
    }
}

public interface IYandexGeocoderClient
{
    Coroutine ReverseGeocode(GeoCoordinate coordinate, Action<YandexReverseGeocodeResult> completed);
    IEnumerator ReverseGeocodeRoutine(GeoCoordinate coordinate, Action<YandexReverseGeocodeResult> completed);
    void SetApiKey(string apiKey);
}

public interface ISpatialMeshYandexBuildingProbe
{
    Coroutine Probe(Vector3 origin, Vector3 direction, Action<SpatialMeshYandexProbeResult> completed);
    IEnumerator ProbeRoutine(Vector3 origin, Vector3 direction, Action<SpatialMeshYandexProbeResult> completed);
    bool TryRaycastSpatialMesh(Vector3 origin, Vector3 direction, out RaycastHit hit);
    GeoCoordinate WorldToGeoCoordinate(Vector3 worldPosition);
}
