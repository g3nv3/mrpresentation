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

[DisallowMultipleComponent]
public sealed class GigaChatClient : MonoBehaviour, IGigaChatClient
{
    private const string DefaultModel = "GigaChat";

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

    private IGigaChat _chat;
    private string _configuredSecretKey;
    private bool _configuredIsCommercial;
    private bool _configuredIgnoreTls;
    private bool _configuredSaveImage;
    private bool _hasToken;

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
