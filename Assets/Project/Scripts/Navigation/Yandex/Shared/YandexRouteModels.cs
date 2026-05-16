using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct GeoCoordinate
{
    [Tooltip("Широта в градусах. Положительные значения находятся севернее экватора.")]
    public double Latitude;

    [Tooltip("Долгота в градусах. Положительные значения находятся восточнее Гринвича.")]
    public double Longitude;

    public GeoCoordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public bool IsValid => Latitude >= -90d && Latitude <= 90d && Longitude >= -180d && Longitude <= 180d;

    public override string ToString()
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", Latitude, Longitude);
    }
}

public enum YandexRouteTravelMode
{
    Driving,
    Truck,
    Walking,
    Transit,
    Bicycle,
    Scooter
}

public sealed class YandexRouteRequest
{
    private readonly List<GeoCoordinate> _waypoints = new List<GeoCoordinate>();

    public YandexRouteTravelMode Mode { get; set; } = YandexRouteTravelMode.Walking;
    public bool? AvoidTolls { get; set; }
    public bool? AvoidUnpaved { get; set; }
    public bool? AvoidPoorCondition { get; set; }
    public string Traffic { get; set; }
    public int Results { get; set; } = 1;
    public IReadOnlyList<GeoCoordinate> Waypoints => _waypoints;

    public YandexRouteRequest()
    {
    }

    public YandexRouteRequest(GeoCoordinate start, GeoCoordinate finish, YandexRouteTravelMode mode = YandexRouteTravelMode.Walking)
    {
        Mode = mode;
        AddWaypoint(start);
        AddWaypoint(finish);
    }

    public YandexRouteRequest(IEnumerable<GeoCoordinate> waypoints, YandexRouteTravelMode mode = YandexRouteTravelMode.Walking)
    {
        Mode = mode;
        SetWaypoints(waypoints);
    }

    public void AddWaypoint(GeoCoordinate waypoint)
    {
        _waypoints.Add(waypoint);
    }

    public void SetWaypoints(IEnumerable<GeoCoordinate> waypoints)
    {
        _waypoints.Clear();

        if (waypoints == null)
        {
            return;
        }

        foreach (var waypoint in waypoints)
        {
            _waypoints.Add(waypoint);
        }
    }
}

public sealed class YandexRouteStep
{
    private readonly List<GeoCoordinate> _points;

    public float LengthMeters { get; }
    public float DurationSeconds { get; }
    public string Mode { get; }
    public IReadOnlyList<GeoCoordinate> Points => _points;

    public YandexRouteStep(IEnumerable<GeoCoordinate> points, float lengthMeters, float durationSeconds, string mode)
    {
        _points = points != null ? new List<GeoCoordinate>(points) : new List<GeoCoordinate>();
        LengthMeters = lengthMeters;
        DurationSeconds = durationSeconds;
        Mode = mode;
    }
}

public sealed class YandexRouteData
{
    private readonly List<GeoCoordinate> _points;
    private readonly List<YandexRouteStep> _steps;

    public IReadOnlyList<GeoCoordinate> Points => _points;
    public IReadOnlyList<YandexRouteStep> Steps => _steps;
    public float TotalLengthMeters { get; }
    public float TotalDurationSeconds { get; }

    public YandexRouteData(
        IEnumerable<GeoCoordinate> points,
        IEnumerable<YandexRouteStep> steps,
        float totalLengthMeters,
        float totalDurationSeconds)
    {
        _points = points != null ? new List<GeoCoordinate>(points) : new List<GeoCoordinate>();
        _steps = steps != null ? new List<YandexRouteStep>(steps) : new List<YandexRouteStep>();
        TotalLengthMeters = totalLengthMeters;
        TotalDurationSeconds = totalDurationSeconds;
    }
}

public sealed class YandexRouteResult
{
    public bool Succeeded { get; }
    public YandexRouteData Route { get; }
    public string Error { get; }
    public string RawJson { get; }
    public long ResponseCode { get; }

    private YandexRouteResult(bool succeeded, YandexRouteData route, string error, string rawJson, long responseCode)
    {
        Succeeded = succeeded;
        Route = route;
        Error = error;
        RawJson = rawJson;
        ResponseCode = responseCode;
    }

    public static YandexRouteResult Success(YandexRouteData route, string rawJson, long responseCode)
    {
        return new YandexRouteResult(true, route, null, rawJson, responseCode);
    }

    public static YandexRouteResult Failure(string error, string rawJson = null, long responseCode = 0)
    {
        return new YandexRouteResult(false, null, error, rawJson, responseCode);
    }
}

public interface IYandexRouteClient
{
    Coroutine RequestRoute(YandexRouteRequest request, Action<YandexRouteResult> completed);
    IEnumerator RequestRouteRoutine(YandexRouteRequest request, Action<YandexRouteResult> completed);
    void SetApiKey(string apiKey);
}

public interface IYandexRoutePresenter
{
    Transform PlayerTransform { get; set; }
    Vector3 RouteOffset { get; set; }
    bool IsVisible { get; }

    bool TryShowRoute(YandexRouteData route);
    bool TryShowRoute(IReadOnlyList<GeoCoordinate> routePoints);
    void DisableRoute();
    void RebuildView();
}

public interface IYandexHelmetNavigator
{
    GeoCoordinate? CurrentCoordinate { get; }
    bool IsRouteVisible { get; }

    void SetCurrentCoordinate(GeoCoordinate coordinate);
    Coroutine ShowRoute(GeoCoordinate start, GeoCoordinate finish, Action<YandexRouteResult> completed = null);
    Coroutine ShowRouteTo(GeoCoordinate finish, Action<YandexRouteResult> completed = null);
    Coroutine RequestAndShowRoute(YandexRouteRequest request, Action<YandexRouteResult> completed = null);
    void DisableRoute();
}

public static class GeoCoordinateUtility
{
    private const double EarthRadiusMeters = 6378137d;

    public static Vector2 GetMetersOffset(GeoCoordinate origin, GeoCoordinate point)
    {
        var originLatitude = origin.Latitude * Mathf.Deg2Rad;
        var pointLatitude = point.Latitude * Mathf.Deg2Rad;
        var deltaLatitude = (point.Latitude - origin.Latitude) * Mathf.Deg2Rad;
        var deltaLongitude = (point.Longitude - origin.Longitude) * Mathf.Deg2Rad;
        var averageLatitude = (originLatitude + pointLatitude) * 0.5d;

        var eastMeters = deltaLongitude * Math.Cos(averageLatitude) * EarthRadiusMeters;
        var northMeters = deltaLatitude * EarthRadiusMeters;

        return new Vector2((float)eastMeters, (float)northMeters);
    }

    public static GeoCoordinate GetCoordinateFromMetersOffset(GeoCoordinate origin, Vector2 metersOffset)
    {
        var originLatitude = origin.Latitude * Mathf.Deg2Rad;
        var latitude = origin.Latitude + metersOffset.y / EarthRadiusMeters * Mathf.Rad2Deg;
        var longitude = origin.Longitude +
                        metersOffset.x / (EarthRadiusMeters * Math.Cos(originLatitude)) * Mathf.Rad2Deg;

        return new GeoCoordinate(latitude, longitude);
    }

    public static string ToYandexModeValue(YandexRouteTravelMode mode)
    {
        switch (mode)
        {
            case YandexRouteTravelMode.Driving:
                return "driving";
            case YandexRouteTravelMode.Truck:
                return "truck";
            case YandexRouteTravelMode.Transit:
                return "transit";
            case YandexRouteTravelMode.Bicycle:
                return "bicycle";
            case YandexRouteTravelMode.Scooter:
                return "scooter";
            case YandexRouteTravelMode.Walking:
            default:
                return "walking";
        }
    }
}
