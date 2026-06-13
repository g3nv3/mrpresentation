using UnityEngine;
using VContainer;

public sealed class DoublePinchTeleportToggle : MonoBehaviour
{
    [SerializeField] private GameObject targetObject;
    [SerializeField] private Transform playerPoint;
    [SerializeField] private PicoHandInput handInputOverride;
    [SerializeField] private bool deactivateOnStart;
    [Tooltip("Если включено, объект получает полный rotation из Player Point. Если выключено, применяется только поворот вокруг Y.")]
    [SerializeField] private bool rotateAroundAllAxes = true;

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
            targetObject.transform.SetPositionAndRotation(targetPose.position, GetTargetRotation(targetPose));
        }

        targetObject.SetActive(nextActive);
    }

    private Quaternion GetTargetRotation(Transform targetPose)
    {
        if (rotateAroundAllAxes)
        {
            return targetPose.rotation;
        }

        var eulerAngles = targetPose.rotation.eulerAngles;
        return Quaternion.Euler(0f, eulerAngles.y, 0f);
    }
}
