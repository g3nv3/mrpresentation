using UnityEngine;

[DisallowMultipleComponent]
public sealed class HardLockToTarget : MonoBehaviour
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 offset;
    [SerializeField] private bool lockOnStart = true;

    public Transform TargetTransform => targetTransform;
    public Vector3 Offset => offset;
    public bool LockOnStart => lockOnStart;

    public void Initialize(Transform target, Vector3 positionOffset)
    {
        targetTransform = target;
        offset = positionOffset;
        ApplyLock();
    }

    public void SetTarget(Transform target)
    {
        targetTransform = target;
        ApplyLock();
    }

    public void SetOffset(Vector3 positionOffset)
    {
        offset = positionOffset;
        ApplyLock();
    }

    private void Start()
    {
        if (lockOnStart)
        {
            ApplyLock();
        }
    }

    private void LateUpdate()
    {
        if (lockOnStart)
        {
            return;
        }

        ApplyLock();
    }

    private void ApplyLock()
    {
        if (targetTransform == null)
        {
            return;
        }

        transform.position = targetTransform.position + offset;
    }
}
