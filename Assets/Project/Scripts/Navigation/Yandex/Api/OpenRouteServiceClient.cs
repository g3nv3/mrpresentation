using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class OpenRouteServiceClient : MonoBehaviour, IYandexRouteClient
{
    private const string DefaultEndpoint = "https://api.openrouteservice.org/v2/directions";

    [Header("OpenRouteService API")]
    [Tooltip("API key from openrouteservice.org/dev. It is sent in the Authorization header.")]
    [SerializeField] private string apiKey;

    [SerializeField] private string endpoint = DefaultEndpoint;
    [SerializeField] private YandexRouteTravelMode defaultMode = YandexRouteTravelMode.Walking;
    [SerializeField, Min(1f)] private float timeoutSeconds = 15f;

    public YandexRouteTravelMode DefaultMode
    {
        get => defaultMode;
        set => defaultMode = value;
    }

    public void SetApiKey(string value)
    {
        apiKey = value;
    }

    public Coroutine RequestRoute(GeoCoordinate start, GeoCoordinate finish, Action<YandexRouteResult> completed)
    {
        return RequestRoute(new YandexRouteRequest(start, finish, defaultMode), completed);
    }

    public Coroutine RequestRoute(IReadOnlyList<GeoCoordinate> waypoints, Action<YandexRouteResult> completed)
    {
        return RequestRoute(new YandexRouteRequest(waypoints, defaultMode), completed);
    }

    public Coroutine RequestRoute(YandexRouteRequest request, Action<YandexRouteResult> completed)
    {
        return StartCoroutine(RequestRouteRoutine(request, completed));
    }

    public IEnumerator RequestRouteRoutine(YandexRouteRequest request, Action<YandexRouteResult> completed)
    {
        var validationError = ValidateRequest(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            completed?.Invoke(YandexRouteResult.Failure(validationError));
            yield break;
        }

        var url = BuildUrl(request.Mode);
        var body = BuildRequestBody(request);

        using (var webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Authorization", apiKey.Trim());
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/geo+json, application/json");
            webRequest.timeout = Mathf.CeilToInt(timeoutSeconds);

            yield return webRequest.SendWebRequest();

            var rawJson = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : null;
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                var apiError = OpenRouteServiceJsonParser.TryReadError(rawJson);
                var error = string.IsNullOrWhiteSpace(apiError) ? webRequest.error : apiError;
                completed?.Invoke(YandexRouteResult.Failure(error, rawJson, webRequest.responseCode));
                yield break;
            }

            if (!OpenRouteServiceJsonParser.TryParse(rawJson, out var route, out var parseError))
            {
                completed?.Invoke(YandexRouteResult.Failure(parseError, rawJson, webRequest.responseCode));
                yield break;
            }

            completed?.Invoke(YandexRouteResult.Success(route, rawJson, webRequest.responseCode));
        }
    }

    private string ValidateRequest(YandexRouteRequest request)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "OpenRouteService API key is empty.";
        }

        if (request == null || request.Waypoints == null || request.Waypoints.Count < 2)
        {
            return "OpenRouteService request must contain at least two waypoints.";
        }

        for (var i = 0; i < request.Waypoints.Count; i++)
        {
            if (!request.Waypoints[i].IsValid)
            {
                return "OpenRouteService request contains invalid waypoint at index " + i + ".";
            }
        }

        return null;
    }

    private string BuildUrl(YandexRouteTravelMode mode)
    {
        return (string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint).TrimEnd('/') +
               "/" + ToProfile(mode) + "/geojson";
    }

    private static string BuildRequestBody(YandexRouteRequest request)
    {
        var builder = new StringBuilder("{\"coordinates\":[");
        for (var i = 0; i < request.Waypoints.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            var point = request.Waypoints[i];
            builder.Append('[')
                .Append(point.Longitude.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(point.Latitude.ToString("R", CultureInfo.InvariantCulture))
                .Append(']');
        }

        builder.Append("],\"instructions\":true,\"geometry\":true}");
        return builder.ToString();
    }

    private static string ToProfile(YandexRouteTravelMode mode)
    {
        switch (mode)
        {
            case YandexRouteTravelMode.Driving:
                return "driving-car";
            case YandexRouteTravelMode.Truck:
                return "driving-hgv";
            case YandexRouteTravelMode.Bicycle:
            case YandexRouteTravelMode.Scooter:
                return "cycling-regular";
            case YandexRouteTravelMode.Transit:
            case YandexRouteTravelMode.Walking:
            default:
                return "foot-walking";
        }
    }
}

internal static class OpenRouteServiceJsonParser
{
    public static bool TryParse(string json, out YandexRouteData route, out string error)
    {
        route = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "OpenRouteService response is empty.";
            return false;
        }

        try
        {
            var root = JsonValueParser.Parse(json) as Dictionary<string, object>;
            var features = GetList(root, "features");
            var feature = features != null && features.Count > 0
                ? features[0] as Dictionary<string, object>
                : null;
            var geometry = GetObject(feature, "geometry");
            var coordinateValues = GetList(geometry, "coordinates");
            var points = ReadCoordinates(coordinateValues);

            if (points.Count < 2)
            {
                error = "OpenRouteService response contains fewer than two route points.";
                return false;
            }

            var properties = GetObject(feature, "properties");
            var summary = GetObject(properties, "summary");
            var totalDistance = GetFloat(summary, "distance");
            var totalDuration = GetFloat(summary, "duration");
            var steps = ReadSteps(properties, points);

            if (steps.Count == 0)
            {
                steps.Add(new YandexRouteStep(points, totalDistance, totalDuration, "openrouteservice"));
            }

            route = new YandexRouteData(points, steps, totalDistance, totalDuration);
            return true;
        }
        catch (Exception exception)
        {
            error = "Failed to parse OpenRouteService response: " + exception.Message;
            return false;
        }
    }

    public static string TryReadError(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var root = JsonValueParser.Parse(json) as Dictionary<string, object>;
            var error = GetObject(root, "error");
            if (error != null && error.TryGetValue("message", out var message))
            {
                return Convert.ToString(message, CultureInfo.InvariantCulture);
            }

            if (root != null && root.TryGetValue("message", out var rootMessage))
            {
                return Convert.ToString(rootMessage, CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Preserve the UnityWebRequest error if the server returned non-JSON content.
        }

        return null;
    }

    private static List<YandexRouteStep> ReadSteps(Dictionary<string, object> properties, List<GeoCoordinate> routePoints)
    {
        var result = new List<YandexRouteStep>();
        var segments = GetList(properties, "segments");
        if (segments == null)
        {
            return result;
        }

        foreach (var segmentValue in segments)
        {
            var segment = segmentValue as Dictionary<string, object>;
            var steps = GetList(segment, "steps");
            if (steps == null)
            {
                continue;
            }

            foreach (var stepValue in steps)
            {
                var step = stepValue as Dictionary<string, object>;
                var indices = GetList(step, "way_points");
                if (step == null || indices == null || indices.Count < 2)
                {
                    continue;
                }

                var start = Mathf.Clamp(Convert.ToInt32(indices[0], CultureInfo.InvariantCulture), 0, routePoints.Count - 1);
                var finish = Mathf.Clamp(Convert.ToInt32(indices[1], CultureInfo.InvariantCulture), start, routePoints.Count - 1);
                var stepPoints = routePoints.GetRange(start, finish - start + 1);
                result.Add(new YandexRouteStep(
                    stepPoints,
                    GetFloat(step, "distance"),
                    GetFloat(step, "duration"),
                    "openrouteservice"));
            }
        }

        return result;
    }

    private static List<GeoCoordinate> ReadCoordinates(List<object> values)
    {
        var result = new List<GeoCoordinate>();
        if (values == null)
        {
            return result;
        }

        foreach (var value in values)
        {
            var pair = value as List<object>;
            if (pair == null || pair.Count < 2)
            {
                continue;
            }

            var longitude = Convert.ToDouble(pair[0], CultureInfo.InvariantCulture);
            var latitude = Convert.ToDouble(pair[1], CultureInfo.InvariantCulture);
            var coordinate = new GeoCoordinate(latitude, longitude);
            if (coordinate.IsValid)
            {
                result.Add(coordinate);
            }
        }

        return result;
    }

    private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key)
    {
        return source != null && source.TryGetValue(key, out var value)
            ? value as Dictionary<string, object>
            : null;
    }

    private static List<object> GetList(Dictionary<string, object> source, string key)
    {
        return source != null && source.TryGetValue(key, out var value) ? value as List<object> : null;
    }

    private static float GetFloat(Dictionary<string, object> source, string key)
    {
        return source != null && source.TryGetValue(key, out var value)
            ? Convert.ToSingle(value, CultureInfo.InvariantCulture)
            : 0f;
    }
}

internal sealed class JsonValueParser
{
    private readonly string _json;
    private int _index;

    private JsonValueParser(string json)
    {
        _json = json;
    }

    public static object Parse(string json)
    {
        return new JsonValueParser(json).ParseValue();
    }

    private object ParseValue()
    {
        SkipWhitespace();
        if (_index >= _json.Length)
        {
            throw new FormatException("Unexpected end of JSON.");
        }

        switch (_json[_index])
        {
            case '{': return ParseObject();
            case '[': return ParseArray();
            case '"': return ParseString();
            case 't': ReadLiteral("true"); return true;
            case 'f': ReadLiteral("false"); return false;
            case 'n': ReadLiteral("null"); return null;
            default: return ParseNumber();
        }
    }

    private Dictionary<string, object> ParseObject()
    {
        var result = new Dictionary<string, object>();
        Expect('{');
        SkipWhitespace();
        if (TryConsume('}')) return result;

        while (true)
        {
            SkipWhitespace();
            var key = ParseString();
            SkipWhitespace();
            Expect(':');
            result[key] = ParseValue();
            SkipWhitespace();
            if (TryConsume('}')) return result;
            Expect(',');
        }
    }

    private List<object> ParseArray()
    {
        var result = new List<object>();
        Expect('[');
        SkipWhitespace();
        if (TryConsume(']')) return result;

        while (true)
        {
            result.Add(ParseValue());
            SkipWhitespace();
            if (TryConsume(']')) return result;
            Expect(',');
        }
    }

    private string ParseString()
    {
        Expect('"');
        var builder = new StringBuilder();
        while (_index < _json.Length)
        {
            var character = _json[_index++];
            if (character == '"') return builder.ToString();
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }

            if (_index >= _json.Length) throw new FormatException("Invalid JSON escape.");
            var escaped = _json[_index++];
            switch (escaped)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (_index + 4 > _json.Length) throw new FormatException("Invalid unicode escape.");
                    builder.Append((char)int.Parse(_json.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    _index += 4;
                    break;
                default: throw new FormatException("Unknown JSON escape: " + escaped);
            }
        }

        throw new FormatException("Unterminated JSON string.");
    }

    private object ParseNumber()
    {
        var start = _index;
        while (_index < _json.Length && "-+0123456789.eE".IndexOf(_json[_index]) >= 0) _index++;
        var token = _json.Substring(start, _index - start);
        if (token.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
        {
            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        return long.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private void ReadLiteral(string literal)
    {
        if (_index + literal.Length > _json.Length ||
            string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
        {
            throw new FormatException("Invalid JSON literal.");
        }

        _index += literal.Length;
    }

    private void SkipWhitespace()
    {
        while (_index < _json.Length && char.IsWhiteSpace(_json[_index])) _index++;
    }

    private bool TryConsume(char expected)
    {
        if (_index >= _json.Length || _json[_index] != expected) return false;
        _index++;
        return true;
    }

    private void Expect(char expected)
    {
        SkipWhitespace();
        if (_index >= _json.Length || _json[_index] != expected)
        {
            throw new FormatException("Expected '" + expected + "' at position " + _index + ".");
        }

        _index++;
    }
}
