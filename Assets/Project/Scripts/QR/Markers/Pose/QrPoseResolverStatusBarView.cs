using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class QrPoseResolverStatusBarView : IQrPoseResolverView
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Image fillImage;
    [SerializeField] private bool hideTargetWhenUnavailable = true;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.04f, 0f);
    [SerializeField, Range(0f, 1f)] private float initialFillAmount;
    [SerializeField, Min(0f)] private float showDuration = 0.15f;
    [SerializeField, Min(0f)] private float hideDuration = 0.12f;
    [SerializeField, Min(0f)] private float fillDuration = 0.08f;

    [NonSerialized] private Vector3 baseScale;
    [NonSerialized] private Vector3 visibleScale;
    [NonSerialized] private bool hasBaseScale;
    [NonSerialized] private bool progressVisible;
    [NonSerialized] private float targetFillAmount;
    [NonSerialized] private Pose? latestPose;
    [NonSerialized] private Tween visibilityTween;
    [NonSerialized] private Tween fillTween;

    public void Initialize(Transform host)
    {
        CacheBaseScale();
        visibleScale = baseScale;
        targetFillAmount = Mathf.Clamp01(initialFillAmount);
        SetFillAmount(targetFillAmount, false);

        if (hideTargetWhenUnavailable && targetTransform != null)
        {
            targetTransform.gameObject.SetActive(false);
        }
    }

    public void Show(Pose? resolvedPose, IReadOnlyList<Vector3> resultPointPositions)
    {
        latestPose = resolvedPose;
        if (!resolvedPose.HasValue || !progressVisible)
        {
            if (hideTargetWhenUnavailable)
            {
                AnimateHide();
            }

            return;
        }

        var pose = resolvedPose.Value;
        ApplyPose(pose);
        AnimateShow();
        SetFillAmount(targetFillAmount, true);
    }

    public void Hide()
    {
        progressVisible = false;
        targetFillAmount = 0f;
        SetFillAmount(targetFillAmount, false);

        if (hideTargetWhenUnavailable)
        {
            AnimateHide();
        }
    }

    public void SetProgress(float normalizedProgress, bool isVisible)
    {
        progressVisible = isVisible;
        targetFillAmount = Mathf.Clamp01(normalizedProgress);
        SetFillAmount(targetFillAmount, true);

        if (!isVisible)
        {
            Hide();
            return;
        }

        if (!latestPose.HasValue)
        {
            return;
        }

        ApplyPose(latestPose.Value);
        AnimateShow();
    }

    private void ApplyPose(in Pose pose)
    {
        if (targetTransform == null)
        {
            return;
        }

        targetTransform.position = pose.position + pose.rotation * localOffset;
        targetTransform.forward = pose.rotation * Vector3.forward;
    }

    private void CacheBaseScale()
    {
        if (hasBaseScale || targetTransform == null)
        {
            return;
        }

        baseScale = targetTransform.localScale;
        visibleScale = baseScale;
        hasBaseScale = true;
    }

    private void AnimateShow()
    {
        if (targetTransform == null)
        {
            return;
        }

        CacheBaseScale();
        if (!targetTransform.gameObject.activeSelf)
        {
            StopVisibilityAnimation();
            targetTransform.gameObject.SetActive(true);

            if (showDuration <= 0f)
            {
                targetTransform.localScale = visibleScale;
                return;
            }

            targetTransform.localScale = Vector3.zero;
            visibilityTween = targetTransform
                .DOScale(visibleScale, showDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => visibilityTween = null);
            return;
        }

        StopVisibilityAnimation();
        targetTransform.localScale = visibleScale;
    }

    private void AnimateHide()
    {
        if (targetTransform == null)
        {
            return;
        }

        CacheBaseScale();
        StopVisibilityAnimation();
        if (!targetTransform.gameObject.activeSelf)
        {
            return;
        }

        if (hideDuration <= 0f)
        {
            targetTransform.gameObject.SetActive(false);
            targetTransform.localScale = visibleScale;
            return;
        }

        visibilityTween = targetTransform
            .DOScale(Vector3.zero, hideDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                targetTransform.gameObject.SetActive(false);
                targetTransform.localScale = visibleScale;
                visibilityTween = null;
            });
    }

    private void StopVisibilityAnimation()
    {
        if (visibilityTween == null)
        {
            return;
        }

        visibilityTween.Kill();
        visibilityTween = null;
    }

    private void StopFillAnimation()
    {
        if (fillTween == null)
        {
            return;
        }

        fillTween.Kill();
        fillTween = null;
    }

    private void SetFillAmount(float fillAmount, bool animated)
    {
        if (fillImage == null)
        {
            return;
        }

        StopFillAnimation();
        if (!animated || fillDuration <= 0f)
        {
            fillImage.fillAmount = fillAmount;
            return;
        }

        fillTween = DOTween
            .To(() => fillImage.fillAmount, value => fillImage.fillAmount = value, fillAmount, fillDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => fillTween = null);
    }
}
