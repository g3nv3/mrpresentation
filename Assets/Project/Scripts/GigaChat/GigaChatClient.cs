using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LD.Sber.GigaChatSDK;
using LD.Sber.GigaChatSDK.Interfaces;
using LD.Sber.GigaChatSDK.Models;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class GigaChatClient : MonoBehaviour, IGigaChatClient
{
    private const string DefaultModel = "GigaChat";
    private const string DefaultAuthEndpoint = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    private const string DefaultModelsEndpoint = "https://gigachat.devices.sberbank.ru/api/v1/models";
    private const string DefaultScope = "GIGACHAT_API_PERS";
    private const string BasicPrefix = "Basic ";

    [Header("GigaChat SDK")]
    [Tooltip("Secret key / Authorization key из кабинета GigaChat API.")]
    [SerializeField] private string secretKey;
    [SerializeField] private bool isCommercial;
    [SerializeField] private bool ignoreTls;
    [SerializeField] private bool saveImage;

    [Header("Prompt")]
    [SerializeField] private string model = DefaultModel;
    [SerializeField, Range(0f, 2f)] private float temperature = 0.45f;
    [SerializeField, Min(32)] private int maxTokens = 350;
    [SerializeField, TextArea(2, 6)] private string systemPrompt =
        "Ты лаконичный VR-гид. Отвечай на русском языке: 2-4 предложения, без списков. " +
        "Если данных об объекте мало, честно скажи, что известно только по геокодеру.";

    [Header("Models Debug")]
    [SerializeField] private bool logModelsOnStart = true;
    [SerializeField] private string authEndpoint = DefaultAuthEndpoint;
    [SerializeField] private string modelsEndpoint = DefaultModelsEndpoint;
    [SerializeField] private string scope = DefaultScope;
    [SerializeField, Min(1f)] private float modelsRequestTimeoutSeconds = 20f;

    private IGigaChat _chat;
    private string _configuredSecretKey;
    private bool _configuredIsCommercial;
    private bool _configuredIgnoreTls;
    private bool _configuredSaveImage;
    private bool _hasToken;

    private void Start()
    {
        if (logModelsOnStart)
        {
            StartCoroutine(LogAvailableModelsRoutine());
        }
    }

    [ContextMenu("Log Available GigaChat Models")]
    public void LogAvailableModels()
    {
        StartCoroutine(LogAvailableModelsRoutine());
    }

    public void SetAuthorizationKey(string value)
    {
        secretKey = value;
        ResetClient();
    }

    public Coroutine GenerateBuildingSummary(
        GigaChatBuildingContext context,
        Action<GigaChatSummaryResult> completed)
    {
        return StartCoroutine(GenerateBuildingSummaryRoutine(context, completed));
    }

    public IEnumerator GenerateBuildingSummaryRoutine(
        GigaChatBuildingContext context,
        Action<GigaChatSummaryResult> completed)
    {
        var validationError = ValidateChatRequest(context);
        if (!string.IsNullOrEmpty(validationError))
        {
            completed?.Invoke(GigaChatSummaryResult.Failure(validationError));
            yield break;
        }

        EnsureClient();

        if (!_hasToken)
        {
            var tokenTask = _chat.CreateTokenAsync();
            yield return WaitForTask(tokenTask);

            if (tokenTask.IsFaulted)
            {
                completed?.Invoke(GigaChatSummaryResult.Failure(GetTaskError(tokenTask)));
                yield break;
            }

            _hasToken = true;
        }

        var completionTask = _chat.CompletionsAsync(BuildQuery(context));
        yield return WaitForTask(completionTask);

        if (completionTask.IsFaulted)
        {
            completed?.Invoke(GigaChatSummaryResult.Failure(GetTaskError(completionTask)));
            yield break;
        }

        var summary = ExtractSummary(completionTask.Result);
        if (string.IsNullOrWhiteSpace(summary))
        {
            completed?.Invoke(GigaChatSummaryResult.Failure("GigaChat response does not contain message content."));
            yield break;
        }

        completed?.Invoke(GigaChatSummaryResult.Success(summary, null, 0));
    }

    public IEnumerator LogAvailableModelsRoutine()
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            Debug.LogWarning("GigaChat models request skipped: secret key is empty.", this);
            yield break;
        }

        string accessToken = null;
        string tokenError = null;
        yield return RequestAccessTokenRoutine(
            token => accessToken = token,
            error => tokenError = error);

        if (!string.IsNullOrWhiteSpace(tokenError))
        {
            Debug.LogWarning("GigaChat models token request failed: " + tokenError, this);
            yield break;
        }

        using (var request = UnityWebRequest.Get(GetEndpoint(modelsEndpoint, DefaultModelsEndpoint)))
        {
            request.timeout = Mathf.CeilToInt(modelsRequestTimeoutSeconds);
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            ApplyCertificateHandler(request);

            yield return request.SendWebRequest();

            var rawJson = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    "GigaChat models request failed: " + request.error + "\n" + rawJson,
                    this);
                yield break;
            }

            if (!TryParseModels(rawJson, out var lines, out var parseError))
            {
                Debug.LogWarning("GigaChat models parse failed: " + parseError + "\n" + rawJson, this);
                yield break;
            }

            Debug.Log("Available GigaChat models:\n" + string.Join("\n", lines), this);
        }
    }

    private IEnumerator RequestAccessTokenRoutine(Action<string> completed, Action<string> failed)
    {
        var requestBody = "scope=" + UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(scope) ? DefaultScope : scope);
        using (var request = new UnityWebRequest(GetEndpoint(authEndpoint, DefaultAuthEndpoint), UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(modelsRequestTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("RqUID", Guid.NewGuid().ToString());
            request.SetRequestHeader("Authorization", BuildBasicAuthorizationHeader());
            ApplyCertificateHandler(request);

            yield return request.SendWebRequest();

            var rawJson = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (request.result != UnityWebRequest.Result.Success)
            {
                failed?.Invoke(request.error + "\n" + rawJson);
                yield break;
            }

            if (!TryParseToken(rawJson, out var accessToken, out var parseError))
            {
                failed?.Invoke(parseError + "\n" + rawJson);
                yield break;
            }

            completed?.Invoke(accessToken);
        }
    }

    private void EnsureClient()
    {
        if (_chat != null &&
            _configuredSecretKey == secretKey &&
            _configuredIsCommercial == isCommercial &&
            _configuredIgnoreTls == ignoreTls &&
            _configuredSaveImage == saveImage)
        {
            return;
        }

        var httpService = new HttpService(ignoreTls);
        var tokenService = new TokenService(httpService, secretKey, isCommercial);
        _chat = new GigaChat(tokenService, httpService, saveImage);
        _configuredSecretKey = secretKey;
        _configuredIsCommercial = isCommercial;
        _configuredIgnoreTls = ignoreTls;
        _configuredSaveImage = saveImage;
        _hasToken = false;
    }

    private void ResetClient()
    {
        _chat = null;
        _configuredSecretKey = null;
        _hasToken = false;
    }

    private string ValidateChatRequest(GigaChatBuildingContext context)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return "GigaChat secret key is empty.";
        }

        if (context == null)
        {
            return "GigaChat building context is empty.";
        }

        if (string.IsNullOrWhiteSpace(context.FullAddress) &&
            string.IsNullOrWhiteSpace(context.ObjectName) &&
            string.IsNullOrWhiteSpace(context.Description))
        {
            return "No object data for GigaChat summary.";
        }

        return null;
    }

    private MessageQuery BuildQuery(GigaChatBuildingContext context)
    {
        var query = new MessageQuery
        {
            Model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            Temperature = temperature,
            MaxTokens = Mathf.Max(32, maxTokens),
            Messages = new List<MessageContent>
            {
                new MessageContent("system", systemPrompt),
                new MessageContent("user", BuildUserPrompt(context))
            }
        };

        return query;
    }

    private static string BuildUserPrompt(GigaChatBuildingContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Подготовь короткую выжимку для человека в VR-шлеме, который указал на городской объект.");
        AppendPromptField(builder, "Название", context.ObjectName);
        AppendPromptField(builder, "Адрес", context.FullAddress);
        AppendPromptField(builder, "Описание геокодера", context.Description);
        AppendPromptField(builder, "Тип объекта", context.Kind);
        AppendPromptField(builder, "Точность геокодера", context.Precision);

        if (context.Coordinate.IsValid)
        {
            builder.Append("Координаты: ");
            builder.Append(context.Coordinate.Latitude.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(", ");
            builder.AppendLine(context.Coordinate.Longitude.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        }

        builder.AppendLine("Не выдумывай исторические факты, если не уверен.");
        return builder.ToString();
    }

    private static void AppendPromptField(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(value.Trim());
    }

    private static IEnumerator WaitForTask(Task task)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }
    }

    private static string GetTaskError(Task task)
    {
        if (task == null)
        {
            return "GigaChat task is empty.";
        }

        if (task.Exception == null)
        {
            return "GigaChat request failed.";
        }

        return task.Exception.GetBaseException().Message;
    }

    private static string ExtractSummary(Response response)
    {
        if (response == null || response.Choices == null)
        {
            return null;
        }

        var choice = response.Choices.LastOrDefault();
        return choice != null && choice.Message != null ? choice.Message.Content : null;
    }

    private string BuildBasicAuthorizationHeader()
    {
        var value = secretKey.Trim();
        if (value.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return BasicPrefix + value;
    }

    private static string GetEndpoint(string configured, string fallback)
    {
        return string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
    }

    private void ApplyCertificateHandler(UnityWebRequest request)
    {
        if (ignoreTls)
        {
            request.certificateHandler = new AcceptAnyCertificateHandler();
        }
    }

    private static bool TryParseToken(string rawJson, out string accessToken, out string error)
    {
        accessToken = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "GigaChat token response is empty.";
            return false;
        }

        try
        {
            var response = JsonUtility.FromJson<GigaChatTokenResponseDto>(rawJson);
            if (response == null || string.IsNullOrWhiteSpace(response.access_token))
            {
                error = "GigaChat token response does not contain access_token.";
                return false;
            }

            accessToken = response.access_token;
            return true;
        }
        catch (Exception exception)
        {
            error = "Failed to parse GigaChat token response: " + exception.Message;
            return false;
        }
    }

    private static bool TryParseModels(string rawJson, out string[] lines, out string error)
    {
        lines = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "GigaChat models response is empty.";
            return false;
        }

        try
        {
            var response = JsonUtility.FromJson<GigaChatModelsResponseDto>(rawJson);
            if (response == null || response.data == null)
            {
                error = "GigaChat models response does not contain data.";
                return false;
            }

            lines = response.data
                .Where(modelInfo => modelInfo != null)
                .Select(modelInfo => modelInfo.id + " / " + modelInfo.owned_by)
                .ToArray();
            return true;
        }
        catch (Exception exception)
        {
            error = "Failed to parse GigaChat models response: " + exception.Message;
            return false;
        }
    }

    private sealed class AcceptAnyCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }

    [Serializable]
    private sealed class GigaChatTokenResponseDto
    {
        public string access_token;
    }

    [Serializable]
    private sealed class GigaChatModelsResponseDto
    {
        public GigaChatModelInfoDto[] data;
    }

    [Serializable]
    private sealed class GigaChatModelInfoDto
    {
        public string id;
        public string owned_by;
    }
}

public interface IGigaChatClient
{
    Coroutine GenerateBuildingSummary(GigaChatBuildingContext context, Action<GigaChatSummaryResult> completed);
    IEnumerator GenerateBuildingSummaryRoutine(GigaChatBuildingContext context, Action<GigaChatSummaryResult> completed);
    void SetAuthorizationKey(string authorizationKey);
}

public sealed class GigaChatBuildingContext
{
    public GeoCoordinate Coordinate { get; }
    public string FullAddress { get; }
    public string ObjectName { get; }
    public string Description { get; }
    public string Kind { get; }
    public string Precision { get; }

    public GigaChatBuildingContext(
        GeoCoordinate coordinate,
        string fullAddress,
        string objectName,
        string description,
        string kind,
        string precision)
    {
        Coordinate = coordinate;
        FullAddress = fullAddress;
        ObjectName = objectName;
        Description = description;
        Kind = kind;
        Precision = precision;
    }

    public static GigaChatBuildingContext FromYandexResult(YandexReverseGeocodeResult result)
    {
        if (result == null)
        {
            return null;
        }

        return new GigaChatBuildingContext(
            result.Coordinate,
            result.FullAddress,
            result.ObjectName,
            result.Description,
            result.Kind,
            result.Precision);
    }
}

public sealed class GigaChatSummaryResult
{
    public bool Succeeded { get; }
    public string Summary { get; }
    public string Error { get; }
    public string RawJson { get; }
    public long ResponseCode { get; }

    private GigaChatSummaryResult(
        bool succeeded,
        string summary,
        string error,
        string rawJson,
        long responseCode)
    {
        Succeeded = succeeded;
        Summary = summary;
        Error = error;
        RawJson = rawJson;
        ResponseCode = responseCode;
    }

    public static GigaChatSummaryResult Success(string summary, string rawJson, long responseCode)
    {
        return new GigaChatSummaryResult(true, summary, null, rawJson, responseCode);
    }

    public static GigaChatSummaryResult Failure(string error, string rawJson = null, long responseCode = 0)
    {
        return new GigaChatSummaryResult(false, null, error, rawJson, responseCode);
    }
}
