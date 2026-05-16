using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class YandexMapsRouteClient : MonoBehaviour, IYandexRouteClient
{
    private const string DefaultEndpoint = "https://api.routing.yandex.net/v2/route";

    [Header("Yandex API")]
    [Tooltip("Ключ Yandex Maps API с доступом к Route API.")]
    [SerializeField] private string apiKey;

    [Tooltip("Endpoint Route API. Оставь значение по умолчанию, если Yandex не изменил endpoint и не используется прокси.")]
    [SerializeField] private string endpoint = DefaultEndpoint;

    [Tooltip("Режим перемещения по умолчанию для упрощенных методов запроса маршрута.")]
    [SerializeField] private YandexRouteTravelMode defaultMode = YandexRouteTravelMode.Walking;

    [Tooltip("Добавляет avoid_tolls=true в запрос маршрута, если конкретный запрос не переопределяет это значение.")]
    [SerializeField] private bool avoidTolls;

    [Tooltip("Добавляет avoid_unpaved=true в запрос маршрута, если конкретный запрос не переопределяет это значение.")]
    [SerializeField] private bool avoidUnpaved;

    [Tooltip("Добавляет avoid_poor_condition=true в запрос маршрута, если конкретный запрос не переопределяет это значение.")]
    [SerializeField] private bool avoidPoorCondition;

    [Tooltip("Отключает учет пробок при построении маршрута.")]
    [SerializeField] private bool disableTraffic;

    [Tooltip("Таймаут сетевого запроса к Route API в секундах.")]
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

        var url = BuildUrl(request);

        using (var webRequest = UnityWebRequest.Get(url))
        {
            webRequest.timeout = Mathf.CeilToInt(timeoutSeconds);
            yield return webRequest.SendWebRequest();

            var rawJson = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : null;
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(YandexRouteResult.Failure(webRequest.error, rawJson, webRequest.responseCode));
                yield break;
            }

            if (!YandexRouteJsonParser.TryParse(rawJson, out var route, out var parseError))
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
            return "Yandex Maps API key is empty.";
        }

        if (request == null)
        {
            return "Yandex route request is null.";
        }

        if (request.Waypoints == null || request.Waypoints.Count < 2)
        {
            return "Yandex route request must contain at least two waypoints.";
        }

        for (var i = 0; i < request.Waypoints.Count; i++)
        {
            if (!request.Waypoints[i].IsValid)
            {
                return "Yandex route request contains invalid waypoint at index " + i + ".";
            }
        }

        return null;
    }

    private string BuildUrl(YandexRouteRequest request)
    {
        var builder = new StringBuilder(string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint);
        builder.Append(builder.ToString().Contains("?") ? "&" : "?");
        AppendQuery(builder, "apikey", apiKey);
        AppendQuery(builder, "waypoints", BuildWaypoints(request.Waypoints));
        AppendQuery(builder, "mode", GeoCoordinateUtility.ToYandexModeValue(request.Mode));

        var trafficValue = request.Traffic;
        if (string.IsNullOrWhiteSpace(trafficValue) && disableTraffic)
        {
            trafficValue = "disabled";
        }

        if (!string.IsNullOrWhiteSpace(trafficValue))
        {
            AppendQuery(builder, "traffic", trafficValue);
        }

        AppendOptionalBool(builder, "avoid_tolls", request.AvoidTolls ?? avoidTolls);
        AppendOptionalBool(builder, "avoid_unpaved", request.AvoidUnpaved ?? avoidUnpaved);
        AppendOptionalBool(builder, "avoid_poor_condition", request.AvoidPoorCondition ?? avoidPoorCondition);

        if (request.Results > 1)
        {
            AppendQuery(builder, "results", request.Results.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string BuildWaypoints(IReadOnlyList<GeoCoordinate> waypoints)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < waypoints.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append(waypoints[i].Latitude.ToString("G17", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(waypoints[i].Longitude.ToString("G17", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void AppendOptionalBool(StringBuilder builder, string key, bool value)
    {
        if (value)
        {
            AppendQuery(builder, key, "true");
        }
    }

    private static void AppendQuery(StringBuilder builder, string key, string value)
    {
        if (builder[builder.Length - 1] != '?' && builder[builder.Length - 1] != '&')
        {
            builder.Append('&');
        }

        builder.Append(UnityWebRequest.EscapeURL(key));
        builder.Append('=');
        builder.Append(UnityWebRequest.EscapeURL(value));
    }
}

internal static class YandexRouteJsonParser
{
    public static bool TryParse(string json, out YandexRouteData route, out string error)
    {
        route = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Yandex route response is empty.";
            return false;
        }

        try
        {
            var parser = new JsonParser(json);
            var rootValue = parser.ParseValue();
            var root = rootValue as Dictionary<string, object>;
            if (root == null)
            {
                error = "Yandex route response root is not an object.";
                return false;
            }

            if (TryGetErrors(root, out error))
            {
                return false;
            }

            var routeNode = GetRouteNode(root);
            if (routeNode == null)
            {
                error = "Yandex route response does not contain route data.";
                return false;
            }

            return TryBuildRoute(routeNode, out route, out error);
        }
        catch (Exception exception)
        {
            error = "Failed to parse Yandex route response: " + exception.Message;
            return false;
        }
    }

    private static bool TryGetErrors(Dictionary<string, object> root, out string error)
    {
        error = null;

        if (!root.TryGetValue("errors", out var errorsValue))
        {
            return false;
        }

        var errors = errorsValue as List<object>;
        if (errors == null || errors.Count == 0)
        {
            error = "Yandex route API returned an error.";
            return true;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < errors.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("; ");
            }

            builder.Append(errors[i]);
        }

        error = builder.ToString();
        return true;
    }

    private static Dictionary<string, object> GetRouteNode(Dictionary<string, object> root)
    {
        if (root.TryGetValue("route", out var routeValue))
        {
            return routeValue as Dictionary<string, object>;
        }

        if (!root.TryGetValue("routes", out var routesValue))
        {
            return null;
        }

        var routes = routesValue as List<object>;
        if (routes == null || routes.Count == 0)
        {
            return null;
        }

        return routes[0] as Dictionary<string, object>;
    }

    private static bool TryBuildRoute(Dictionary<string, object> routeNode, out YandexRouteData route, out string error)
    {
        route = null;
        error = null;

        if (!routeNode.TryGetValue("legs", out var legsValue))
        {
            error = "Yandex route response does not contain legs.";
            return false;
        }

        var legs = legsValue as List<object>;
        if (legs == null || legs.Count == 0)
        {
            error = "Yandex route response contains no legs.";
            return false;
        }

        var routePoints = new List<GeoCoordinate>();
        var routeSteps = new List<YandexRouteStep>();
        var totalLength = 0f;
        var totalDuration = 0f;

        foreach (var legValue in legs)
        {
            var leg = legValue as Dictionary<string, object>;
            if (leg == null)
            {
                continue;
            }

            if (leg.TryGetValue("status", out var statusValue) &&
                statusValue is string status &&
                !string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!leg.TryGetValue("steps", out var stepsValue))
            {
                continue;
            }

            var steps = stepsValue as List<object>;
            if (steps == null)
            {
                continue;
            }

            foreach (var stepValue in steps)
            {
                var step = stepValue as Dictionary<string, object>;
                if (step == null || !TryReadStep(step, out var routeStep))
                {
                    continue;
                }

                routeSteps.Add(routeStep);
                totalLength += routeStep.LengthMeters;
                totalDuration += routeStep.DurationSeconds;
                AppendDistinct(routePoints, routeStep.Points);
            }
        }

        if (routePoints.Count < 2)
        {
            error = "Yandex route response contains fewer than two route points.";
            return false;
        }

        route = new YandexRouteData(routePoints, routeSteps, totalLength, totalDuration);
        return true;
    }

    private static bool TryReadStep(Dictionary<string, object> step, out YandexRouteStep routeStep)
    {
        routeStep = null;

        if (!step.TryGetValue("polyline", out var polylineValue))
        {
            return false;
        }

        var polyline = polylineValue as Dictionary<string, object>;
        if (polyline == null || !polyline.TryGetValue("points", out var pointsValue))
        {
            return false;
        }

        var pointArrays = pointsValue as List<object>;
        if (pointArrays == null || pointArrays.Count == 0)
        {
            return false;
        }

        var points = new List<GeoCoordinate>();
        foreach (var pointArrayValue in pointArrays)
        {
            var pointArray = pointArrayValue as List<object>;
            if (pointArray == null || pointArray.Count < 2)
            {
                continue;
            }

            var latitude = Convert.ToDouble(pointArray[0], CultureInfo.InvariantCulture);
            var longitude = Convert.ToDouble(pointArray[1], CultureInfo.InvariantCulture);
            points.Add(new GeoCoordinate(latitude, longitude));
        }

        if (points.Count == 0)
        {
            return false;
        }

        var length = TryGetFloat(step, "length", out var lengthValue) ? lengthValue : 0f;
        var duration = TryGetFloat(step, "duration", out var durationValue) ? durationValue : 0f;
        var mode = step.TryGetValue("mode", out var modeValue) ? modeValue as string : null;

        routeStep = new YandexRouteStep(points, length, duration, mode);
        return true;
    }

    private static bool TryGetFloat(Dictionary<string, object> source, string key, out float value)
    {
        value = 0f;

        if (!source.TryGetValue(key, out var rawValue))
        {
            return false;
        }

        value = Convert.ToSingle(rawValue, CultureInfo.InvariantCulture);
        return true;
    }

    private static void AppendDistinct(List<GeoCoordinate> target, IReadOnlyList<GeoCoordinate> source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (target.Count > 0)
            {
                var last = target[target.Count - 1];
                var current = source[i];
                if (Math.Abs(last.Latitude - current.Latitude) < 0.0000001d &&
                    Math.Abs(last.Longitude - current.Longitude) < 0.0000001d)
                {
                    continue;
                }
            }

            target.Add(source[i]);
        }
    }

    private sealed class JsonParser
    {
        private readonly string _json;
        private int _index;

        public JsonParser(string json)
        {
            _json = json;
        }

        public object ParseValue()
        {
            SkipWhitespace();

            if (_index >= _json.Length)
            {
                throw new FormatException("Unexpected end of JSON.");
            }

            var character = _json[_index];
            if (character == '{')
            {
                return ParseObject();
            }

            if (character == '[')
            {
                return ParseArray();
            }

            if (character == '"')
            {
                return ParseString();
            }

            if (character == 't' || character == 'f')
            {
                return ParseBool();
            }

            if (character == 'n')
            {
                ParseLiteral("null");
                return null;
            }

            return ParseNumber();
        }

        private Dictionary<string, object> ParseObject()
        {
            var result = new Dictionary<string, object>();
            Expect('{');
            SkipWhitespace();

            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                var key = ParseString();
                SkipWhitespace();
                Expect(':');
                var value = ParseValue();
                result[key] = value;
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object> ParseArray()
        {
            var result = new List<object>();
            Expect('[');
            SkipWhitespace();

            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    return result;
                }

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
                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character != '\\')
                {
                    builder.Append(character);
                    continue;
                }

                if (_index >= _json.Length)
                {
                    throw new FormatException("Unexpected end of JSON string escape.");
                }

                var escape = _json[_index++];
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escape);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ParseUnicodeEscape());
                        break;
                    default:
                        throw new FormatException("Unsupported JSON string escape: " + escape);
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (_index + 4 > _json.Length)
            {
                throw new FormatException("Invalid JSON unicode escape.");
            }

            var hex = _json.Substring(_index, 4);
            _index += 4;
            return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private bool ParseBool()
        {
            if (TryConsumeLiteral("true"))
            {
                return true;
            }

            ParseLiteral("false");
            return false;
        }

        private double ParseNumber()
        {
            var start = _index;

            if (_json[_index] == '-')
            {
                _index++;
            }

            while (_index < _json.Length && char.IsDigit(_json[_index]))
            {
                _index++;
            }

            if (_index < _json.Length && _json[_index] == '.')
            {
                _index++;
                while (_index < _json.Length && char.IsDigit(_json[_index]))
                {
                    _index++;
                }
            }

            if (_index < _json.Length && (_json[_index] == 'e' || _json[_index] == 'E'))
            {
                _index++;
                if (_index < _json.Length && (_json[_index] == '+' || _json[_index] == '-'))
                {
                    _index++;
                }

                while (_index < _json.Length && char.IsDigit(_json[_index]))
                {
                    _index++;
                }
            }

            var token = _json.Substring(start, _index - start);
            return double.Parse(token, CultureInfo.InvariantCulture);
        }

        private void ParseLiteral(string literal)
        {
            if (!TryConsumeLiteral(literal))
            {
                throw new FormatException("Expected JSON literal: " + literal);
            }
        }

        private bool TryConsumeLiteral(string literal)
        {
            if (_index + literal.Length > _json.Length)
            {
                return false;
            }

            for (var i = 0; i < literal.Length; i++)
            {
                if (_json[_index + i] != literal[i])
                {
                    return false;
                }
            }

            _index += literal.Length;
            return true;
        }

        private bool TryConsume(char expected)
        {
            if (_index >= _json.Length || _json[_index] != expected)
            {
                return false;
            }

            _index++;
            return true;
        }

        private void Expect(char expected)
        {
            if (!TryConsume(expected))
            {
                throw new FormatException("Expected '" + expected + "' at index " + _index + ".");
            }
        }

        private void SkipWhitespace()
        {
            while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
            {
                _index++;
            }
        }
    }
}
