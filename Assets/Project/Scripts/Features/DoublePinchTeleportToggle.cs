using UnityEngine;
using VContainer;

public sealed class DoublePinchTeleportToggle : MonoBehaviour
{
    [SerializeField] private GameObject targetObject;
    [SerializeField] private Transform playerPoint;
    [SerializeField] private PicoHandInput handInputOverride;
    [SerializeField] private bool deactivateOnStart;

    private IPicoHandInput _handInput;

    [Inject]
    public void Construct(IPicoHandInput handInput)
    {
        if (handInputOverride != null)
        {
            return;
        }

        SetHandInput(handInput);
    }

    private void Awake()
    {
        if (handInputOverride != null)
        {
            SetHandInput(handInputOverride);
        }

        if (targetObject != null && deactivateOnStart)
        {
            targetObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_handInput != null)
        {
            _handInput.DoublePinchStarted -= HandleDoublePinchStarted;
        }
    }

    private void SetHandInput(IPicoHandInput handInput)
    {
        if (_handInput == handInput)
        {
            return;
        }

        if (_handInput != null)
        {
            _handInput.DoublePinchStarted -= HandleDoublePinchStarted;
        }

        _handInput = handInput;

        if (_handInput != null)
        {
            _handInput.DoublePinchStarted += HandleDoublePinchStarted;
        }
    }

    private void HandleDoublePinchStarted(HandPointerTarget _)
    {
        if (targetObject == null)
        {
            return;
        }

        var nextActive = !targetObject.activeSelf;
        if (nextActive)
        {
            var targetPose = playerPoint != null ? playerPoint : transform;
            targetObject.transform.SetPositionAndRotation(targetPose.position, targetPose.rotation);
        }

        targetObject.SetActive(nextActive);
    }
}
