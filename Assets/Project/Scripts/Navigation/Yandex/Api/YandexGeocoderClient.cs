using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class YandexGeocoderClient : MonoBehaviour, IYandexGeocoderClient
{
    private const string DefaultEndpoint = "https://geocode-maps.yandex.ru/v1";
    private const string HouseKind = "house";

    [Header("Yandex Geocoder API")]
    [Tooltip("Ключ Yandex Maps API с доступом к Geocoder API.")]
    [SerializeField] private string apiKey;

    [Tooltip("Endpoint Geocoder API. Оставь значение по умолчанию, если Yandex не изменил endpoint и не используется прокси.")]
    [SerializeField] private string endpoint = DefaultEndpoint;

    [Tooltip("Язык ответа, например ru_RU или en_US.")]
    [SerializeField] private string language = "ru_RU";

    [Tooltip("Тип объекта для reverse geocode. Значение 'house' просит Yandex вернуть здания и адреса.")]
    [SerializeField] private string reverseKind = HouseKind;

    [Tooltip("Максимальное количество результатов, запрашиваемых у геокодера.")]
    [SerializeField, Min(1)] private int results = 1;

    [Tooltip("Таймаут сетевого запроса к геокодеру в секундах.")]
    [SerializeField, Min(1f)] private float timeoutSeconds = 10f;

    public void SetApiKey(string value)
    {
        apiKey = value;
    }

    public Coroutine ReverseGeocode(GeoCoordinate coordinate, Action<YandexReverseGeocodeResult> completed)
    {
        return StartCoroutine(ReverseGeocodeRoutine(coordinate, completed));
    }

    public IEnumerator ReverseGeocodeRoutine(GeoCoordinate coordinate, Action<YandexReverseGeocodeResult> completed)
    {
        var validationError = ValidateRequest(coordinate);
        if (!string.IsNullOrEmpty(validationError))
        {
            completed?.Invoke(YandexReverseGeocodeResult.Failure(coordinate, validationError));
            yield break;
        }

        using (var request = UnityWebRequest.Get(BuildReverseGeocodeUrl(coordinate)))
        {
            request.timeout = Mathf.CeilToInt(timeoutSeconds);
            yield return request.SendWebRequest();

            var rawJson = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (request.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(YandexReverseGeocodeResult.Failure(coordinate, request.error, rawJson, request.responseCode));
                yield break;
            }

            if (!YandexGeocoderJsonParser.TryParse(coordinate, rawJson, out var result, out var parseError))
            {
                completed?.Invoke(YandexReverseGeocodeResult.Failure(coordinate, parseError, rawJson, request.responseCode));
                yield break;
            }

            completed?.Invoke(YandexReverseGeocodeResult.Success(
                coordinate,
                result.IsBuilding,
                result.FullAddress,
                result.ObjectName,
                result.Description,
                result.Kind,
                result.Precision,
                rawJson,
                request.responseCode));
        }
    }

    private string ValidateRequest(GeoCoordinate coordinate)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "Yandex Geocoder API key is empty.";
        }

        if (!coordinate.IsValid)
        {
            return "Reverse geocode coordinate is invalid.";
        }

        if (string.IsNullOrWhiteSpace(language))
        {
            return "Yandex Geocoder language is empty.";
        }

        return null;
    }

    private string BuildReverseGeocodeUrl(GeoCoordinate coordinate)
    {
        var builder = new StringBuilder(string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint);
        builder.Append(builder.ToString().Contains("?") ? "&" : "?");
        AppendQuery(builder, "apikey", apiKey);
        AppendQuery(builder, "geocode", BuildLongLatCoordinate(coordinate));
        AppendQuery(builder, "lang", language);
        AppendQuery(builder, "format", "json");
        AppendQuery(builder, "results", Mathf.Max(1, results).ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(reverseKind))
        {
            AppendQuery(builder, "kind", reverseKind);
        }

        return builder.ToString();
    }

    private static string BuildLongLatCoordinate(GeoCoordinate coordinate)
    {
        return coordinate.Longitude.ToString("G17", CultureInfo.InvariantCulture) +
               "," +
               coordinate.Latitude.ToString("G17", CultureInfo.InvariantCulture);
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

    private sealed class ParsedGeocode
    {
        public bool IsBuilding;
        public string FullAddress;
        public string ObjectName;
        public string Description;
        public string Kind;
        public string Precision;
    }

    private static class YandexGeocoderJsonParser
    {
        public static bool TryParse(
            GeoCoordinate coordinate,
            string json,
            out ParsedGeocode result,
            out string error)
        {
            result = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Yandex Geocoder response is empty.";
                return false;
            }

            try
            {
                var dto = JsonUtility.FromJson<YandexGeocoderResponseDto>(json);
                var collection = dto != null && dto.response != null ? dto.response.GeoObjectCollection : null;
                if (collection == null || collection.featureMember == null || collection.featureMember.Length == 0)
                {
                    result = new ParsedGeocode();
                    return true;
                }

                var geoObject = collection.featureMember[0] != null ? collection.featureMember[0].GeoObject : null;
                var metadata = geoObject != null &&
                               geoObject.metaDataProperty != null
                    ? geoObject.metaDataProperty.GeocoderMetaData
                    : null;

                if (metadata == null)
                {
                    result = new ParsedGeocode();
                    return true;
                }

                var address = metadata.Address != null ? metadata.Address.formatted : null;
                result = new ParsedGeocode
                {
                    IsBuilding = string.Equals(metadata.kind, HouseKind, StringComparison.OrdinalIgnoreCase),
                    FullAddress = !string.IsNullOrWhiteSpace(address) ? address : metadata.text,
                    ObjectName = geoObject != null ? geoObject.name : null,
                    Description = geoObject != null ? geoObject.description : null,
                    Kind = metadata.kind,
                    Precision = metadata.precision
                };

                return true;
            }
            catch (Exception exception)
            {
                error = "Failed to parse Yandex Geocoder response for " + coordinate + ": " + exception.Message;
                return false;
            }
        }
    }

    [Serializable]
    private sealed class YandexGeocoderResponseDto
    {
        public YandexGeocoderResponseRootDto response;
    }

    [Serializable]
    private sealed class YandexGeocoderResponseRootDto
    {
        public YandexGeoObjectCollectionDto GeoObjectCollection;
    }

    [Serializable]
    private sealed class YandexGeoObjectCollectionDto
    {
        public YandexFeatureMemberDto[] featureMember;
    }

    [Serializable]
    private sealed class YandexFeatureMemberDto
    {
        public YandexGeoObjectDto GeoObject;
    }

    [Serializable]
    private sealed class YandexGeoObjectDto
    {
        public YandexGeocoderMetaDataPropertyDto metaDataProperty;
        public string name;
        public string description;
    }

    [Serializable]
    private sealed class YandexGeocoderMetaDataPropertyDto
    {
        public YandexGeocoderMetaDataDto GeocoderMetaData;
    }

    [Serializable]
    private sealed class YandexGeocoderMetaDataDto
    {
        public string precision;
        public string text;
        public string kind;
        public YandexAddressDto Address;
    }

    [Serializable]
    private sealed class YandexAddressDto
    {
        public string formatted;
    }
}
