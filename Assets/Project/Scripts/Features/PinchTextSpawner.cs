using UnityEngine;
using VContainer;

public class PinchTextSpawner : MonoBehaviour
{
    [SerializeField] private GameObject textPrefab;
    [SerializeField] private string targetTag = "MRMesh";
    [SerializeField] private float surfaceOffset = 0.03f;

    private IPicoHandInput _handInput;

    [Inject]
    public void Construct(IPicoHandInput handInput)
    {
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

    private void HandlePinchStarted()
    {
        if (!_handInput.HasRaycastHit || textPrefab == null)
            return;

        RaycastHit hit = _handInput.RaycastHit;
        if (!hit.collider.CompareTag(targetTag))
            return;

        Instantiate(
            textPrefab,
            hit.point + hit.normal * surfaceOffset,
            Quaternion.LookRotation(-hit.normal));
    }
}
