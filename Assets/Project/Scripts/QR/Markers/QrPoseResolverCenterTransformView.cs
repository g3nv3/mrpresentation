using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[Serializable]
public sealed class QrPoseResolverCenterTransformView : IQrPoseResolverView
{
    private const float MinAxisMagnitude = 0.0001f;
    private const float InverseSqrt2 = 0.70710678f;

    [SerializeField] private Transform targetTransform;
    [SerializeField] private bool hideTargetWhenUnavailable = true;
    [SerializeField, Min(0.01f)] private float showDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float hideDuration = 0.12f;
    [SerializeField, Min(0.1f)] private float pulseDuration = 0.8f;
    [SerializeField, Range(1f, 1.5f)] private float pulseScaleMultiplier = 1.06f;
    [SerializeField, Min(0.01f)] private float sizeMultiplier = 1f;

    [NonSerialized] private Vector3 baseScale;
    [NonSerialized] private Vector3 visibleScale;
    [NonSerialized] private bool hasBaseScale;
    [NonSerialized] private Tween visibilityTween;
    [NonSerialized] private Tween pulseTween;

    public void Initialize(Transform host)
    {
        CacheBaseScale();
        visibleScale = baseScale;
    }

    public void Show(Pose? resolvedPose, IReadOnlyList<Vector3> resultPointPositions)
    {
        if (!resolvedPose.HasValue)
        {
            if (hideTargetWhenUnavailable)
            {
                AnimateHide();
            }

            return;
        }

        var pose = resolvedPose.Value;
        targetTransform.position = pose.position;
        targetTransform.forward = pose.rotation * Vector3.forward;
        UpdateVisibleScale(resultPointPositions);
        AnimateShow();
    }

    public void Hide()
    {
        if (hideTargetWhenUnavailable)
        {
            AnimateHide();
            return;
        }

        StopPulseAnimation();
    }

    private void CacheBaseScale()
    {
        if (hasBaseScale)
        {
            return;
        }

        baseScale = targetTransform.localScale;
        hasBaseScale = true;
    }

    private void AnimateShow()
    {
        CacheBaseScale();
        if (!targetTransform.gameObject.activeSelf)
        {
            StopVisibilityAnimation();
            targetTransform.gameObject.SetActive(true);
            targetTransform.localScale = Vector3.zero;

            visibilityTween = targetTransform
                .DOScale(visibleScale, showDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(StartPulseAnimation);
            return;
        }

        StopVisibilityAnimation();
        targetTransform.localScale = visibleScale;
        StartPulseAnimation();
    }

    private void AnimateHide()
    {
        CacheBaseScale();
        StopVisibilityAnimation();
        StopPulseAnimation();
        if (!targetTransform.gameObject.activeSelf)
        {
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

    private void StartPulseAnimation()
    {
        StopPulseAnimation();
        visibilityTween = null;
        if (!targetTransform.gameObject.activeSelf)
        {
            return;
        }

        pulseTween = targetTransform
            .DOScale(visibleScale * pulseScaleMultiplier, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
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

    private void StopPulseAnimation()
    {
        if (pulseTween == null)
        {
            return;
        }

        pulseTween.Kill();
        pulseTween = null;
    }

    private void UpdateVisibleScale(IReadOnlyList<Vector3> resultPointPositions)
    {
        CacheBaseScale();
        if (!TryGetQrSideLength(resultPointPositions, out var sideLengthMeters))
        {
            visibleScale = baseScale;
            return;
        }

        visibleScale = baseScale * (sideLengthMeters * sizeMultiplier);
    }

    private static bool TryGetQrSideLength(IReadOnlyList<Vector3> resultPointPositions, out float sideLengthMeters)
    {
        sideLengthMeters = 0f;
        if (resultPointPositions == null || resultPointPositions.Count < 3)
        {
            return false;
        }

        var diagonalLength = Vector3.Distance(resultPointPositions[0], resultPointPositions[2]);
        if (diagonalLength < MinAxisMagnitude)
        {
            return false;
        }

        sideLengthMeters = diagonalLength * InverseSqrt2;
        return true;
    }
}
