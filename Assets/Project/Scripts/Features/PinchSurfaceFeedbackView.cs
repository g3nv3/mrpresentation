using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PinchSurfaceFeedbackView : MonoBehaviour
{
    [SerializeField] private GameObject hitPrefab;
    [SerializeField] private Transform surfaceReticle;
    [SerializeField, Min(0f)] private float showDuration = 0.12f;
    [SerializeField, Min(0f)] private float visibleDuration = 0.55f;
    [SerializeField, Min(0f)] private float hideDuration = 0.18f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;
    
    private GameObject _hitObject;

    public void ShowHit(Pose pose)
    {
        if (hitPrefab == null)
        {
            return;
        }

        var instance = Instantiate(hitPrefab, pose.position, pose.rotation);
        var visibleScale = _hitObject.transform.localScale;
        var instanceTransform = instance.transform;
        instanceTransform.localScale = Vector3.zero;

        DOTween.Sequence()
            .Append(instanceTransform.DOScale(visibleScale, showDuration).SetEase(showEase))
            .AppendInterval(visibleDuration)
            .Append(instanceTransform.DOScale(Vector3.zero, hideDuration).SetEase(hideEase))
            .OnComplete(() =>
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                }
            })
            .SetTarget(instanceTransform);
    }
    

    public void SetSurfaceReticle(Pose pose)
    {
        if (surfaceReticle == null)
        {
            return;
        }

        surfaceReticle.SetPositionAndRotation(pose.position, pose.rotation);
        SetSurfaceReticleVisible(true);
    }

    public void SetSurfaceReticleVisible(bool visible)
    {
        if (surfaceReticle != null && surfaceReticle.gameObject.activeSelf != visible)
        {
            surfaceReticle.gameObject.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        SetSurfaceReticleVisible(false);
    }
}
