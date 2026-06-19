using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QrMarkerInstallNotificationPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageLabel;

    [Header("Content")]
    [SerializeField] private string installedMessage = "Объект установлен";

    [Header("Placement")]
    [SerializeField] private Vector3 qrOffset = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private bool offsetInQrSpace = true;
    [SerializeField] private bool faceMainCamera = true;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float showDuration = 0.2f;
    [SerializeField, Min(0f)] private float visibleDuration = 1.5f;
    [SerializeField, Min(0f)] private float hideDuration = 0.18f;
    [SerializeField] private Vector3 popupStartOffset = new Vector3(0f, -0.04f, 0f);
    [SerializeField] private Vector3 hiddenScale = Vector3.one * 0.85f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    private Vector3 visibleScale;
    private bool hasVisibleScale;
    private bool isShowing;
    private Sequence sequence;

    private Transform PanelRoot => panelRoot != null ? panelRoot : transform;

    private void Awake()
    {
        EnsureReferences();
        CacheVisibleScale();
        if (!isShowing)
        {
            HideImmediate();
        }
    }

    private void OnDisable()
    {
        KillSequence();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    public void ShowInstalled(in Pose qrPose, string markerId = null)
    {
        Show(qrPose, installedMessage);
    }

    public void Show(in Pose qrPose, string message)
    {
        EnsureReferences();
        CacheVisibleScale();

        var root = PanelRoot;
        var targetPosition = GetTargetPosition(qrPose);
        var startPosition = targetPosition + popupStartOffset;

        if (messageLabel != null)
        {
            messageLabel.text = string.IsNullOrWhiteSpace(message) ? installedMessage : message;
        }

        isShowing = true;
        root.gameObject.SetActive(true);
        root.position = startPosition;
        root.localScale = GetHiddenScale();
        OrientToCamera();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        KillSequence();
        sequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(root.DOMove(targetPosition, showDuration).SetEase(showEase))
            .Join(root.DOScale(visibleScale, showDuration).SetEase(showEase));

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad));
        }

        if (visibleDuration > 0f)
        {
            sequence.AppendInterval(visibleDuration);
        }

        sequence
            .Append(root.DOScale(GetHiddenScale(), hideDuration).SetEase(hideEase));

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        }

        sequence.OnComplete(() =>
        {
            root.gameObject.SetActive(false);
            root.localScale = visibleScale;
            isShowing = false;
            sequence = null;
        });
    }

    public void Hide()
    {
        EnsureReferences();
        CacheVisibleScale();
        KillSequence();

        var root = PanelRoot;
        if (!root.gameObject.activeSelf)
        {
            return;
        }

        sequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(root.DOScale(GetHiddenScale(), hideDuration).SetEase(hideEase));

        if (canvasGroup != null)
        {
            sequence.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        }

        sequence.OnComplete(() =>
        {
            root.gameObject.SetActive(false);
            root.localScale = visibleScale;
            isShowing = false;
            sequence = null;
        });
    }

    public void HideImmediate()
    {
        EnsureReferences();
        CacheVisibleScale();
        KillSequence();

        var root = PanelRoot;
        isShowing = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        root.localScale = visibleScale;
        root.gameObject.SetActive(false);
    }

    private Vector3 GetTargetPosition(in Pose qrPose)
    {
        return qrPose.position + (offsetInQrSpace ? qrPose.rotation * qrOffset : qrOffset);
    }

    private Vector3 GetHiddenScale()
    {
        return Vector3.Scale(visibleScale, hiddenScale);
    }

    private void OrientToCamera()
    {
        if (!faceMainCamera)
        {
            return;
        }

        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        var root = PanelRoot;
        var direction = root.position - mainCamera.transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        root.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void CacheVisibleScale()
    {
        if (hasVisibleScale)
        {
            return;
        }

        visibleScale = PanelRoot.localScale;
        hasVisibleScale = true;
    }

    private void EnsureReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = transform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }

        if (messageLabel == null)
        {
            messageLabel = panelRoot.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void KillSequence()
    {
        if (sequence == null)
        {
            return;
        }

        if (sequence.IsActive())
        {
            sequence.Kill(false);
        }

        sequence = null;
    }
}
