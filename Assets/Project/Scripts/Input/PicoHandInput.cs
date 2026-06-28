using System;
using Project.Scripts.Interaction;
using UnityEngine;
using Unity.XR.PXR;

public interface IPicoHandInput
{
    bool IsTracked { get; }
    bool PinchHeld { get; }
    HandPointerTarget CurrentTarget { get; }
    Vector3 ContactPosition { get; }
    Quaternion ContactRotation { get; }
    bool TryGetCurrentContact(out HandContactTarget target);

    event Action<HandPointerTarget> PinchStarted;
    event Action<HandPointerTarget> DoublePinchStarted;
}

public class PicoHandInput : MonoBehaviour, IPicoHandInput
{
    private static readonly HandJoint[] FingerTipJoints =
    {
        HandJoint.JointIndexTip,
        HandJoint.JointMiddleTip,
        HandJoint.JointRingTip,
        HandJoint.JointLittleTip
    };

    private static readonly HandJoint[] ContactTipJoints =
    {
        HandJoint.JointThumbTip,
        HandJoint.JointIndexTip,
        HandJoint.JointMiddleTip,
        HandJoint.JointRingTip,
        HandJoint.JointLittleTip
    };

    [SerializeField] private HandType handType = HandType.HandRight;
    [SerializeField] private bool useStrictTrackingValidation = true;
    [SerializeField] private bool usePinchMidpointForContactPosition = true;
    [SerializeField] private float pinchStartDistanceThreshold = 0.028f;
    [SerializeField] private float pinchReleaseDistanceThreshold = 0.034f;
    [SerializeField] private float doublePinchMaxIntervalSeconds = 0.35f;
    [SerializeField] private bool requirePalmUpForPinch = true;
    [SerializeField, Range(0f, 180f)] private float palmUpMaxAngleDegrees = 60f;
    [SerializeField] private Transform palmUpDirectionTransform;
    [SerializeField] private float rayDistance = 20f;
    [SerializeField] private LayerMask rayMask;
    [SerializeField] private float contactRadius = 0.018f;
    [SerializeField] private bool highlightRayTargetWhenNoContact = true;
    [SerializeField] private float rayHighlightMaxDistance = 0.12f;
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
    public Quaternion ContactRotation { get; private set; } = Quaternion.identity;
    public Ray AimRay { get; private set; }
    public bool HasRaycastHit { get; private set; }
    public RaycastHit RaycastHit { get; private set; }
    public HandPointerTarget CurrentTarget { get; private set; }
    public HandContactTarget CurrentContactTarget { get; private set; }

    public event Action<HandPointerTarget> PinchStarted;
    public event Action<HandPointerTarget> DoublePinchStarted;
    public event Action<HandContactTarget> ContactStarted;
    public event Action<HandContactTarget> ContactEnded;

    private readonly Collider[] _contactHits = new Collider[16];
    private static readonly HandLocationStatus RequiredPositionFlags =
        HandLocationStatus.PositionTracked | HandLocationStatus.PositionValid;
    private readonly Vector3[] _contactTipPoints = new Vector3[5];
    private int _contactTipCount;
    private HandPinchDraggableEffect _currentContactDraggableEffect;
    private HandContactTarget _lastContactTarget;
    private bool _wasPinching;
    private float _lastPinchStartedTime = float.NegativeInfinity;

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
        ContactPosition = usePinchMidpointForContactPosition ? PinchPosition : indexTip;
        UpdateRaycast();
        UpdateContactTarget();
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
        ContactRotation = Quaternion.identity;
        AimRay = default;
        HasRaycastHit = false;
        RaycastHit = default;
        CurrentTarget = default;
        CurrentContactTarget = default;
        _contactTipCount = 0;

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

        var poseJoint = FindClosestPinchFingerTipJoint(joints, out var poseJointPosition);
        UpdateContactTipPoints(joints);

        if (useStrictTrackingValidation &&
            !IsStrictPinchPoseValid(joints, poseJoint))
        {
            return false;
        }

        thumbTip = ToUnityPos(joints.jointLocations[(int)HandJoint.JointThumbTip].pose.Position);
        indexTip = poseJointPosition;
        ContactRotation = ToUnityRot(joints.jointLocations[(int)HandJoint.JointWrist].pose.Orientation);
        var pinchDistance = Vector3.Distance(indexTip, thumbTip);
        var releaseThreshold = Mathf.Max(pinchStartDistanceThreshold, pinchReleaseDistanceThreshold);
        isPinching = _wasPinching
            ? pinchDistance <= releaseThreshold
            : pinchDistance <= pinchStartDistanceThreshold;

        if (isPinching && !IsPalmUpPinchAllowed(ContactRotation))
        {
            isPinching = false;
        }

        return true;
    }

    private bool IsPalmUpPinchAllowed(Quaternion wristRotation)
    {
        if (!requirePalmUpForPinch)
        {
            return true;
        }

        if (palmUpDirectionTransform == null)
        {
            return false;
        }

        var localPalmUpDirection = palmUpDirectionTransform.localRotation * Vector3.down;
        var palmUpDirection = wristRotation * localPalmUpDirection;
        return Vector3.Angle(palmUpDirection, Vector3.up) <= palmUpMaxAngleDegrees;
    }

    private bool IsStrictPinchPoseValid(HandJointLocations joints, HandJoint selectedPinchJoint)
    {
        if (joints.isActive == 0U)
        {
            return false;
        }

        if (!HasRequiredPositionStatus(joints, HandJoint.JointWrist))
        {
            return false;
        }

        if (!HasRequiredPositionStatus(joints, HandJoint.JointThumbTip))
        {
            return false;
        }

        return HasRequiredPositionStatus(joints, selectedPinchJoint);
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

    private HandJoint FindClosestPinchFingerTipJoint(HandJointLocations joints, out Vector3 tipPosition)
    {
        var thumbPosition = ToUnityPos(joints.jointLocations[(int)HandJoint.JointThumbTip].pose.Position);
        var bestJoint = HandJoint.JointIndexTip;
        var bestDistance = float.PositiveInfinity;
        tipPosition = ToUnityPos(joints.jointLocations[(int)bestJoint].pose.Position);

        for (int i = 0; i < FingerTipJoints.Length; i++)
        {
            var joint = FingerTipJoints[i];
            if (useStrictTrackingValidation && !HasRequiredPositionStatus(joints, joint))
            {
                continue;
            }

            var candidatePosition = ToUnityPos(joints.jointLocations[(int)joint].pose.Position);
            var distance = (candidatePosition - thumbPosition).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestJoint = joint;
            tipPosition = candidatePosition;
        }

        return bestJoint;
    }

    private void UpdateContactTipPoints(HandJointLocations joints)
    {
        _contactTipCount = 0;
        for (int i = 0; i < ContactTipJoints.Length; i++)
        {
            var joint = ContactTipJoints[i];
            if (useStrictTrackingValidation && !HasRequiredPositionStatus(joints, joint))
            {
                continue;
            }

            _contactTipPoints[_contactTipCount++] = ToUnityPos(joints.jointLocations[(int)joint].pose.Position);
        }
    }

    private void UpdatePinchState(bool isPinching)
    {
        PinchDown = isPinching && !_wasPinching;
        PinchHeld = isPinching;
        PinchUp = !isPinching && _wasPinching;

        if (PinchDown)
        {
            PinchStarted?.Invoke(CurrentTarget);

            if (Time.unscaledTime - _lastPinchStartedTime <= doublePinchMaxIntervalSeconds)
            {
                DoublePinchStarted?.Invoke(CurrentTarget);
                _lastPinchStartedTime = float.NegativeInfinity;
            }
            else
            {
                _lastPinchStartedTime = Time.unscaledTime;
            }
        }

        _wasPinching = isPinching;
    }

    private void ReleasePinchIfNeeded()
    {
        PinchHeld = false;

        if (!_wasPinching)
            return;

        PinchUp = true;
        _wasPinching = false;
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

    public bool TryGetCurrentContact(out HandContactTarget target)
    {
        target = CurrentContactTarget;
        return target.HasHit;
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

    private void UpdateContactTarget()
    {
        var bestDistance = float.PositiveInfinity;
        Collider bestCollider = null;
        var bestPoint = Vector3.zero;

        if (_contactTipCount == 0)
        {
            EvaluateContact(ContactPosition, ref bestDistance, ref bestCollider, ref bestPoint);
        }
        else
        {
            for (int i = 0; i < _contactTipCount; i++)
            {
                EvaluateContact(_contactTipPoints[i], ref bestDistance, ref bestCollider, ref bestPoint);
            }
        }

        CurrentContactTarget = bestCollider == null
            ? default
            : new HandContactTarget(true, bestCollider, bestPoint);

        HandPinchDraggableEffect nextDraggableEffect = null;
        if (!CurrentContactTarget.TryGetComponentInParent(out nextDraggableEffect) &&
            highlightRayTargetWhenNoContact &&
            CurrentTarget.HasHit &&
            (CurrentTarget.Hit.point - ContactPosition).sqrMagnitude <= rayHighlightMaxDistance * rayHighlightMaxDistance)
        {
            CurrentTarget.TryGetComponentInParent(out nextDraggableEffect);
        }

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
            if (!TryGetSafeClosestPoint(collider, samplePosition, out var closestPoint))
            {
                continue;
            }

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

    private static bool TryGetSafeClosestPoint(Collider collider, Vector3 samplePosition, out Vector3 closestPoint)
    {
        closestPoint = default;

        if (collider == null)
        {
            return false;
        }

        if (collider is MeshCollider meshCollider && !meshCollider.convex)
        {
            return false;
        }

        closestPoint = collider.ClosestPoint(samplePosition);
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!debugContactGizmo)
        {
            return;
        }

        var previousColor = Gizmos.color;
        Gizmos.color = debugContactGizmoColor;

        if (_contactTipCount > 0)
        {
            for (int i = 0; i < _contactTipCount; i++)
            {
                Gizmos.DrawWireSphere(_contactTipPoints[i], contactRadius);
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, contactRadius);
        }

        Gizmos.color = previousColor;
    }
}
