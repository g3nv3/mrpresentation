using UnityEngine;
using VContainer;

public class PinchTextSpawner : MonoBehaviour
{
    [SerializeField] private GameObject textPrefab;
    [SerializeField] private string targetTag = "MRMesh";
    [SerializeField] private float surfaceOffset = 0.03f;
    [SerializeField] private bool active = false;

    private IPicoHandInput _handInput;

    [Inject]
    public void Construct(IPicoHandInput handInput)
    {
        if(!active) return;
        
        if (_handInput != null)
            _handInput.PinchStarted -= HandlePinchStarted;

        _handInput = handInput;
        _handInput.PinchStarted += HandlePinchStarted;
    }

    private void OnDestroy()
    {
        if (_handInput == null)
            return;

        _handInput.PinchStarted -= HandlePinchStarted;
    }

    private void HandlePinchStarted(HandPointerTarget target)
    {
        if (textPrefab == null || !active)
            return;

        if (!target.TryGetSurfacePose(targetTag, surfaceOffset, out var pose))
            return;

        Instantiate(
            textPrefab,
            pose.position,
            pose.rotation);
    }
}
