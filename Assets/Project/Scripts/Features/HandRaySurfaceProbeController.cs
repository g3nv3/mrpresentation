using UnityEngine;
using VContainer;
using Project.Scripts.UI;

[DisallowMultipleComponent]
public sealed class HandRaySurfaceProbeController : MonoBehaviour, IUiToggleState
{
    [SerializeField] private PinchSurfaceFeedbackView feedbackView;
    [SerializeField] private SpatialMeshYandexBuildingProbe yandexBuildingProbe;
    [SerializeField] private string targetTag = "MRMesh";
    [SerializeField] private float surfaceOffset = 0.03f;
    [SerializeField] private bool active = false;
    [SerializeField] private bool probeYandexOnPinch = true;
    [SerializeField] private bool logYandexResult = true;

    private IPicoHandInput _handInput;
    public bool IsOn => active;
    public event System.Action<bool> Changed;

    private void Update()
    {
        if (feedbackView == null)
        {
            return;
        }

        if (!active ||
            _handInput == null ||
            !_handInput.IsTracked ||
            !_handInput.CurrentTarget.TryGetSurfacePose(targetTag, surfaceOffset, out var pose))
        {
            feedbackView.SetSurfaceReticleVisible(false);
            return;
        }

        feedbackView.SetSurfaceReticle(pose);
    }

    [Inject]
    public void Construct(IPicoHandInput handInput)
    {
        if (_handInput != null)
        {
            _handInput.PinchStarted -= HandlePinchStarted;
        }

        _handInput = handInput;

        if (_handInput != null)
        {
            _handInput.PinchStarted += HandlePinchStarted;
        }
    }

    public void ToggleActive()
    {
        Toggle();
    }

    public void Toggle()
    {
        SetOn(!active);
    }

    public void SetOn(bool value)
    {
        if (active == value)
        {
            return;
        }

        active = value;
        if (!active)
        {
            feedbackView?.SetSurfaceReticleVisible(false);
        }

        Changed?.Invoke(active);
    }

    private void OnDestroy()
    {
        if (_handInput == null)
        {
            return;
        }

        _handInput.PinchStarted -= HandlePinchStarted;
    }

    private void HandlePinchStarted(HandPointerTarget target)
    {
        if (!active || !target.TryGetSurfacePose(targetTag, surfaceOffset, out var pose))
        {
            return;
        }

        if (feedbackView != null)
        {
            feedbackView.ShowHit(pose);
        }

        if (!probeYandexOnPinch || yandexBuildingProbe == null)
        {
            return;
        }

        yandexBuildingProbe.ProbeHit(target.Hit, HandleYandexProbeCompleted);
    }

    private void HandleYandexProbeCompleted(SpatialMeshYandexProbeResult result)
    {
        if (!logYandexResult)
        {
            return;
        }

        if (result == null)
        {
            Debug.LogWarning("Yandex building probe returned no result.", this);
            return;
        }

        if (!result.HasSpatialMeshHit)
        {
            Debug.Log("Yandex building probe: no spatial mesh hit.", this);
            return;
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.LogWarning("Yandex building probe failed: " + result.Error, this);
            return;
        }

        if (result.IsBuilding)
        {
            Debug.Log("Yandex building probe: building, " + result.FullAddress, this);
            return;
        }

        var kind = result.GeocodeResult != null ? result.GeocodeResult.Kind : "unknown";
        Debug.Log("Yandex building probe: not confirmed as building. Kind: " + kind, this);
    }
}
