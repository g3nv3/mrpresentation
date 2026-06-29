using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GigaChatSurfaceGuideController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GigaChatClient gigaChatClient;
    [SerializeField] private GigaChatWorldCanvasView canvasView;

    [Header("Messages")]
    [SerializeField] private string geocoderStatusMessage = "Определяю объект...";
    [SerializeField] private string gigaChatStatusMessage = "Готовлю краткую справку...";
    [SerializeField] private string noHitMessage = "Не удалось определить поверхность.";
    [SerializeField] private string noObjectMessage = "Не удалось получить данные об объекте.";
    [SerializeField] private string requestFailedMessage = "Не удалось получить справку GigaChat.";
    [SerializeField] private bool showTechnicalErrors = true;

    private Coroutine _summaryRoutine;
    private int _requestVersion;

    private void Awake()
    {
        if (gigaChatClient == null)
        {
            gigaChatClient = GetComponent<GigaChatClient>();
        }

        if (canvasView == null)
        {
            canvasView = GetComponent<GigaChatWorldCanvasView>();
        }
    }

    public void SetDependencies(GigaChatClient client, GigaChatWorldCanvasView view)
    {
        gigaChatClient = client;
        canvasView = view;
    }

    public void ShowPendingAtHit(RaycastHit hit)
    {
        canvasView?.ShowAt(hit, geocoderStatusMessage);
    }

    public void HandleYandexProbeResult(SpatialMeshYandexProbeResult result)
    {
        _requestVersion++;
        var version = _requestVersion;

        if (_summaryRoutine != null)
        {
            StopCoroutine(_summaryRoutine);
            _summaryRoutine = null;
        }

        if (result == null)
        {
            SetMessage(noObjectMessage);
            return;
        }

        if (!result.HasSpatialMeshHit)
        {
            SetMessage(noHitMessage);
            return;
        }

        canvasView?.ShowAt(result.Hit, gigaChatStatusMessage);

        if (!string.IsNullOrEmpty(result.Error))
        {
            SetMessage(WithTechnicalError(noObjectMessage, result.Error));
            return;
        }

        if (result.GeocodeResult == null || !result.GeocodeResult.Succeeded)
        {
            var error = result.GeocodeResult != null ? result.GeocodeResult.Error : null;
            SetMessage(WithTechnicalError(noObjectMessage, error));
            return;
        }

        if (gigaChatClient == null)
        {
            SetMessage(WithTechnicalError(requestFailedMessage, "GigaChat client is not assigned."));
            return;
        }

        var context = GigaChatBuildingContext.FromYandexResult(result.GeocodeResult);
        _summaryRoutine = StartCoroutine(RequestSummaryRoutine(context, version));
    }

    public void Hide()
    {
        canvasView?.Hide();
    }

    private IEnumerator RequestSummaryRoutine(GigaChatBuildingContext context, int version)
    {
        GigaChatSummaryResult summaryResult = null;
        yield return gigaChatClient.GenerateBuildingSummaryRoutine(context, result => summaryResult = result);

        if (version != _requestVersion)
        {
            yield break;
        }

        _summaryRoutine = null;

        if (summaryResult == null)
        {
            SetMessage(requestFailedMessage);
            yield break;
        }

        if (!summaryResult.Succeeded)
        {
            SetMessage(WithTechnicalError(requestFailedMessage, summaryResult.Error));
            yield break;
        }

        SetMessage(summaryResult.Summary);
    }

    private void SetMessage(string message)
    {
        canvasView?.SetMessage(message);
    }

    private string WithTechnicalError(string publicMessage, string technicalError)
    {
        if (!showTechnicalErrors || string.IsNullOrWhiteSpace(technicalError))
        {
            return publicMessage;
        }

        return publicMessage + "\n" + technicalError;
    }
}
