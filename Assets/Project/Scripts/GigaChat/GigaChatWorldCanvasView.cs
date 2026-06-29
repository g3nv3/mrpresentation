using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GigaChatWorldCanvasView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform canvasRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private Transform cameraTransform;

    [Header("Placement")]
    [Tooltip("Смещение от поверхности по нормали, чтобы canvas не оказался внутри стены.")]
    [SerializeField, Min(0f)] private float surfaceNormalOffset = 0.18f;
    [SerializeField] private Vector3 additionalWorldOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private bool faceCamera = true;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float showDuration = 0.22f;
    [SerializeField, Min(0f)] private float hideDuration = 0.16f;
    [SerializeField, Range(0.01f, 1f)] private float hiddenScaleMultiplier = 0.82f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;
    [SerializeField] private bool hideOnAwake = true;

    private Tween _animation;
    private Vector3 _visibleScale;

    private Transform Root => canvasRoot != null ? canvasRoot : transform;

    private void Awake()
    {
        if (canvasRoot == null)
        {
            canvasRoot = transform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = canvasRoot.GetComponent<CanvasGroup>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        _visibleScale = Root.localScale;

        if (hideOnAwake)
        {
            Root.gameObject.SetActive(false);
            Root.localScale = _visibleScale * hiddenScaleMultiplier;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }

    public void ShowAt(RaycastHit hit, string message)
    {
        var normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        var position = hit.point + normal * surfaceNormalOffset + additionalWorldOffset;
        ShowAt(position, normal, message);
    }

    public void ShowAt(Vector3 position, Vector3 surfaceNormal, string message)
    {
        var root = Root;
        root.position = position;
        root.rotation = GetRotation(position, surfaceNormal);
        SetMessage(message);
        Show();
    }

    public void SetMessage(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }
    }

    public void Show()
    {
        var root = Root;
        _animation?.Kill();
        root.gameObject.SetActive(true);
        root.localScale = _visibleScale * hiddenScaleMultiplier;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        var sequence = DOTween.Sequence()
            .Append(root.DOScale(_visibleScale, showDuration).SetEase(showEase));

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(1f, showDuration));
        }

        sequence.OnComplete(() =>
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        });

        _animation = sequence.SetTarget(root);
    }

    public void Hide()
    {
        var root = Root;
        if (!root.gameObject.activeSelf)
        {
            return;
        }

        _animation?.Kill();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        var sequence = DOTween.Sequence()
            .Append(root.DOScale(_visibleScale * hiddenScaleMultiplier, hideDuration).SetEase(hideEase));

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(0f, hideDuration));
        }

        sequence.OnComplete(() => root.gameObject.SetActive(false));
        _animation = sequence.SetTarget(root);
    }

    private Quaternion GetRotation(Vector3 position, Vector3 surfaceNormal)
    {
        if (faceCamera && cameraTransform != null)
        {
            var toCanvas = position - cameraTransform.position;
            toCanvas.y = 0f;

            if (toCanvas.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(toCanvas.normalized, Vector3.up);
            }
        }

        var normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.forward;
        return Quaternion.LookRotation(normal, Vector3.up);
    }

    private void OnDisable()
    {
        _animation?.Kill();
        _animation = null;
    }
}
