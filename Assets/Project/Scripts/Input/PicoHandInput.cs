using System;
using Project.Scripts.Interaction;
using UnityEngine;
using Unity.XR.PXR;

public interface IPicoHandInput
{
    bool IsTracked { get; }
    bool PinchDown { get; }
    bool PinchHeld { get; }
    bool PinchUp { get; }
    Vector3 PinchPosition { get; }
    Vector3 ContactPosition { get; }
    Ray AimRay { get; }
    bool HasRaycastHit { get; }
    RaycastHit RaycastHit { get; }
    HandPointerTarget CurrentTarget { get; }
    HandContactTarget CurrentContactTarget { get; }

    bool TryGetCurrentTarget(out HandPointerTarget target);
    bool TryGetCurrentTarget(string requiredTag, out HandPointerTarget target);
    bool TryGetCurrentContact(out HandContactTarget target);
    bool TryGetCurrentSurfacePose(float surfaceOffset, out Pose pose);
    bool TryGetCurrentSurfacePose(string requiredTag, float surfaceOffset, out Pose pose);

    event Action<HandPointerTarget> PinchStarted;
    event Action<HandPointerTarget> PinchEnded;
    event Action<HandContactTarget> ContactStarted;
    event Action<HandContactTarget> ContactEnded;
}

public class PicoHandInput : MonoBehaviour, IPicoHandInput
{
    [SerializeField] private HandType handType = HandType.HandRight;
    [SerializeField] private bool useStrictTrackingValidation = true;
    [SerializeField] private float rayDistance = 20f;
    [SerializeField] private LayerMask rayMask;
    [SerializeField] private Transform contactPoint;
    [SerializeField] private float contactRadius = 0.018f;
    [SerializeField] private LayerMask contactMask = Physics.DefaultRaycastLayers;
    [SerializeField] private bool debugContactGizmo = true;
    [SerializeField] private Color debugContactGizmoColor = new Color(0.1f, 0.9f, 0.4f, 0.9f);
    [SerializeField] private bool debugSphere;
    [SerializeField] private Transform sphere;

    public bool IsTracked { get; private set; }
    public bool PinchDown { get; private set; }
    public bool PinchHeld { get; private set; }
    public bool PinchUp { get; private set; }
    public Vector3 PinchPosition { get; private set; }
    public Vector3 ContactPosition { get; private set; }
    public Ray AimRay { get; private set; }
    public bool HasRaycastHit { get; private set; }
    public RaycastHit RaycastHit { get; private set; }
    public HandPointerTarget CurrentTarget { get; private set; }
    public HandContactTarget CurrentContactTarget { get; private set; }

    public event Action<HandPointerTarget> PinchStarted;
    public event Action<HandPointerTarget> PinchEnded;
    public event Action<HandContactTarget> ContactStarted;
    public event Action<HandContactTarget> ContactEnded;

    private readonly Collider[] _contactHits = new Collider[16];
    private static readonly HandLocationStatus RequiredPositionFlags =
        HandLocationStatus.PositionTracked | HandLocationStatus.PositionValid;
    private HandPinchDraggableEffect _currentContactDraggableEffect;
    private HandContactTarget _lastContactTarget;
    private bool _wasPinching;

    private void Start()
    {
        if (sphere != null)
        {
            sphere.gameObject.SetActive(debugSphere);
        }
    }

    private void Update()
    {
        ResetFrameState();

        if (!TryReadHandState(out bool isPinching, out Vector3 indexTip, out Vector3 thumbTip))
        {
            ReleaseContactIfNeeded();
            ReleasePinchIfNeeded();
            return;
        }

        IsTracked = true;
        PinchPosition = Vector3.Lerp(indexTip, thumbTip, 0.5f);
        ContactPosition = contactPoint != null ? contactPoint.position : PinchPosition;
        UpdateRaycast();
        UpdateContactTarget(ContactPosition);
        UpdateContactState();
        // Subscribers to PinchStarted read the current hit immediately.
        UpdatePinchState(isPinching);
    }

    private void ResetFrameState()
    {
        IsTracked = false;
        PinchDown = false;
        PinchUp = false;
        PinchPosition = default;
        ContactPosition = default;
        AimRay = default;
        HasRaycastHit = false;
        RaycastHit = default;
        CurrentTarget = default;
        CurrentContactTarget = default;

        if (_currentContactDraggableEffect != null)
        {
            _currentContactDraggableEffect.HideGrabEffect();
            _currentContactDraggableEffect = null;
        }
    }

    private bool TryReadHandState(out bool isPinching, out Vector3 indexTip, out Vector3 thumbTip)
    {
        isPinching = false;
        indexTip = default;
        thumbTip = default;

        HandJointLocations joints = new HandJointLocations();
        if (!PXR_HandTracking.GetJointLocations(handType, ref joints))
            return false;

        if (joints.jointLocations == null || joints.jointLocations.Length == 0)
            return false;

        if (useStrictTrackingValidation &&
            (joints.isActive == 0U ||
             !HasRequiredPositionStatus(joints, HandJoint.JointWrist) ||
             !HasRequiredPositionStatus(joints, HandJoint.JointIndexTip) ||
             !HasRequiredPositionStatus(joints, HandJoint.JointThumbTip)))
        {
            return false;
        }

        HandAimState aimState = new HandAimState();
        if (!PXR_HandTracking.GetAimState(handType, ref aimState))
            return false;

        if (useStrictTrackingValidation && (aimState.aimStatus & HandAimStatus.AimComputed) == 0)
            return false;

        indexTip = ToUnityPos(joints.jointLocations[(int)HandJoint.JointIndexTip].pose.Position);
        thumbTip = ToUnityPos(joints.jointLocations[(int)HandJoint.JointThumbTip].pose.Position);
        isPinching = (aimState.aimStatus & HandAimStatus.AimIndexPinching) != 0 ||
                     (aimState.aimStatus & HandAimStatus.AimMiddlePinching) != 0 ||
                     (aimState.aimStatus & HandAimStatus.AimRingPinching) != 0 ||
                     (aimState.aimStatus & HandAimStatus.AimLittlePinching) != 0 ||
                     (aimState.aimStatus & HandAimStatus.AimRayTouched) != 0;

        return true;
    }

    private static bool HasRequiredPositionStatus(HandJointLocations joints, HandJoint joint)
    {
        int index = (int)joint;
        if (index < 0 || index >= joints.jointLocations.Length)
        {
            return false;
        }

        var status = joints.jointLocations[index].locationStatus;
        return (status & RequiredPositionFlags) == RequiredPositionFlags;
    }

    private void UpdatePinchState(bool isPinching)
    {
        PinchDown = isPinching && !_wasPinching;
        PinchHeld = isPinching;
        PinchUp = !isPinching && _wasPinching;

        if (PinchDown)
            PinchStarted?.Invoke(CurrentTarget);

        if (PinchUp)
            PinchEnded?.Invoke(CurrentTarget);

        _wasPinching = isPinching;
    }

    private void ReleasePinchIfNeeded()
    {
        PinchHeld = false;

        if (!_wasPinching)
            return;

        PinchUp = true;
        _wasPinching = false;
        PinchEnded?.Invoke(CurrentTarget);
    }

    private void ReleaseContactIfNeeded()
    {
        if (!_lastContactTarget.HasHit || _lastContactTarget.Collider == null)
        {
            return;
        }

        ContactEnded?.Invoke(_lastContactTarget);
        _lastContactTarget = default;
    }

    private void UpdateRaycast()
    {
        if (!TryReadAimRay(out var ray))
        {
            AimRay = default;
            CurrentTarget = new HandPointerTarget(default, false, default);
            return;
        }

        AimRay = ray;

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, rayMask))
        {
            CurrentTarget = new HandPointerTarget(ray, false, default);
            return;
        }

        HasRaycastHit = true;
        RaycastHit = hit;
        CurrentTarget = new HandPointerTarget(ray, true, hit);

        if (sphere != null)
            sphere.position = hit.point;
    }

    public bool TryGetCurrentTarget(out HandPointerTarget target)
    {
        target = CurrentTarget;
        return target.HasHit;
    }

    public bool TryGetCurrentTarget(string requiredTag, out HandPointerTarget target)
    {
        target = CurrentTarget;
        return target.HasTag(requiredTag);
    }

    public bool TryGetCurrentContact(out HandContactTarget target)
    {
        target = CurrentContactTarget;
        return target.HasHit;
    }

    public bool TryGetCurrentSurfacePose(float surfaceOffset, out Pose pose)
    {
        return CurrentTarget.TryGetSurfacePose(surfaceOffset, out pose);
    }

    public bool TryGetCurrentSurfacePose(string requiredTag, float surfaceOffset, out Pose pose)
    {
        return CurrentTarget.TryGetSurfacePose(requiredTag, surfaceOffset, out pose);
    }

    private Vector3 ToUnityPos(Vector3f p)
    {
        return new Vector3(p.x, p.y, -p.z);
    }

    private Quaternion ToUnityRot(Quatf q)
    {
        return new Quaternion(-q.x, -q.y, q.z, q.w);
    }

    private bool TryReadAimRay(out Ray ray)
    {
        ray = default;

        HandAimState aimState = new HandAimState();
        if (!PXR_HandTracking.GetAimState(handType, ref aimState))
        {
            return false;
        }

        if (useStrictTrackingValidation &&
            ((aimState.aimStatus & HandAimStatus.AimComputed) == 0 ||
             (aimState.aimStatus & HandAimStatus.AimRayValid) == 0))
        {
            return false;
        }

        Vector3 rayOrigin = ToUnityPos(aimState.aimRayPose.Position);
        Quaternion rayRotation = ToUnityRot(aimState.aimRayPose.Orientation);
        Vector3 rayDirection = (rayRotation * Vector3.forward).normalized;
        rayOrigin += rayDirection * 0.02f;
        ray = new Ray(rayOrigin, rayDirection);
        return true;
    }

    private void UpdateContactTarget(Vector3 samplePosition)
    {
        var bestDistance = float.PositiveInfinity;
        Collider bestCollider = null;
        var bestPoint = Vector3.zero;

        EvaluateContact(samplePosition, ref bestDistance, ref bestCollider, ref bestPoint);

        CurrentContactTarget = bestCollider == null
            ? default
            : new HandContactTarget(true, bestCollider, bestPoint);

        HandPinchDraggableEffect nextDraggableEffect = null;
        CurrentContactTarget.TryGetComponentInParent(out nextDraggableEffect);
        if (_currentContactDraggableEffect == nextDraggableEffect)
        {
            return;
        }

        _currentContactDraggableEffect?.HideGrabEffect();
        _currentContactDraggableEffect = nextDraggableEffect;
        _currentContactDraggableEffect?.ShowGrabEffect();
    }

    private void UpdateContactState()
    {
        var hadContact = _lastContactTarget.HasHit && _lastContactTarget.Collider != null;
        var hasContact = CurrentContactTarget.HasHit && CurrentContactTarget.Collider != null;
        var sameCollider = hadContact &&
                           hasContact &&
                           _lastContactTarget.Collider == CurrentContactTarget.Collider;

        if (hadContact && !sameCollider)
        {
            ContactEnded?.Invoke(_lastContactTarget);
        }

        if (hasContact && !sameCollider)
        {
            ContactStarted?.Invoke(CurrentContactTarget);
        }

        _lastContactTarget = hasContact ? CurrentContactTarget : default;
    }

    private void EvaluateContact(
        Vector3 samplePosition,
        ref float bestDistance,
        ref Collider bestCollider,
        ref Vector3 bestPoint)
    {
        var hitCount = Physics.OverlapSphereNonAlloc(
            samplePosition,
            contactRadius,
            _contactHits,
            contactMask.value == 0 ? Physics.DefaultRaycastLayers : contactMask,
            QueryTriggerInteraction.Ignore);

        for (var i = 0; i < hitCount; i++)
        {
            var collider = _contactHits[i];
            if (collider == null)
            {
                continue;
            }

            var closestPoint = collider.ClosestPoint(samplePosition);
            var distance = (closestPoint - samplePosition).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestCollider = collider;
            bestPoint = closestPoint;
        }
    }

    private void OnDrawGizmos()
    {
        if (!debugContactGizmo)
        {
            return;
        }

        var gizmoPosition = contactPoint != null ? contactPoint.position : transform.position;
        var previousColor = Gizmos.color;
        Gizmos.color = debugContactGizmoColor;
        Gizmos.DrawWireSphere(gizmoPosition, contactRadius);
        Gizmos.color = previousColor;
    }
}
