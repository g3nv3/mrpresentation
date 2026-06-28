using System;
using Project.Scripts.Interaction;
using UnityEngine;
using Unity.XR.PXR;

public interface IPicoHandInput
{
    bool IsTracked { get; }
    bool PinchHeld { get; }
    bool RawPinchHeld { get; }
    HandPointerTarget CurrentTarget { get; }
    Vector3 ContactPosition { get; }
    Quaternion ContactRotation { get; }
    bool TryGetCurrentContact(out HandContactTarget target);

    event Action<HandPointerTarget> RawPinchStarted;
    event Action<HandPointerTarget> RawDoublePinchStarted;
    event Action<PicoPinchFinger, HandPointerTarget> RawFingerPinchStarted;
    event Action<PicoPinchFinger, HandPointerTarget> RawFingerDoublePinchStarted;
    event Action<HandPointerTarget> RawIndexPinchStarted;
    event Action<HandPointerTarget> RawMiddlePinchStarted;
    event Action<HandPointerTarget> RawRingPinchStarted;
    event Action<HandPointerTarget> RawLittlePinchStarted;
    event Action<HandPointerTarget> RawIndexDoublePinchStarted;
    event Action<HandPointerTarget> RawMiddleDoublePinchStarted;
    event Action<HandPointerTarget> RawRingDoublePinchStarted;
    event Action<HandPointerTarget> RawLittleDoublePinchStarted;

    event Action<HandPointerTarget> PinchStarted;
    event Action<HandPointerTarget> DoublePinchStarted;
    event Action<PicoPinchFinger, HandPointerTarget> FingerPinchStarted;
    event Action<PicoPinchFinger, HandPointerTarget> FingerDoublePinchStarted;
    event Action<HandPointerTarget> IndexPinchStarted;
    event Action<HandPointerTarget> MiddlePinchStarted;
    event Action<HandPointerTarget> RingPinchStarted;
    event Action<HandPointerTarget> LittlePinchStarted;
    event Action<HandPointerTarget> IndexDoublePinchStarted;
    event Action<HandPointerTarget> MiddleDoublePinchStarted;
    event Action<HandPointerTarget> RingDoublePinchStarted;
    event Action<HandPointerTarget> LittleDoublePinchStarted;
}

public enum PicoPinchFinger
{
    Index,
    Middle,
    Ring,
    Little
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
    public bool RawPinchDown { get; private set; }
    public bool RawPinchHeld { get; private set; }
    public bool RawPinchUp { get; private set; }
    public Vector3 PinchPosition { get; private set; }
    public Vector3 ContactPosition { get; private set; }
    public Quaternion ContactRotation { get; private set; } = Quaternion.identity;
    public Ray AimRay { get; private set; }
    public bool HasRaycastHit { get; private set; }
    public RaycastHit RaycastHit { get; private set; }
    public HandPointerTarget CurrentTarget { get; private set; }
    public HandContactTarget CurrentContactTarget { get; private set; }

    public event Action<HandPointerTarget> RawPinchStarted;
    public event Action<HandPointerTarget> RawDoublePinchStarted;
    public event Action<PicoPinchFinger, HandPointerTarget> RawFingerPinchStarted;
    public event Action<PicoPinchFinger, HandPointerTarget> RawFingerDoublePinchStarted;
    public event Action<HandPointerTarget> RawIndexPinchStarted;
    public event Action<HandPointerTarget> RawMiddlePinchStarted;
    public event Action<HandPointerTarget> RawRingPinchStarted;
    public event Action<HandPointerTarget> RawLittlePinchStarted;
    public event Action<HandPointerTarget> RawIndexDoublePinchStarted;
    public event Action<HandPointerTarget> RawMiddleDoublePinchStarted;
    public event Action<HandPointerTarget> RawRingDoublePinchStarted;
    public event Action<HandPointerTarget> RawLittleDoublePinchStarted;

    public event Action<HandPointerTarget> PinchStarted;
    public event Action<HandPointerTarget> DoublePinchStarted;
    public event Action<PicoPinchFinger, HandPointerTarget> FingerPinchStarted;
    public event Action<PicoPinchFinger, HandPointerTarget> FingerDoublePinchStarted;
    public event Action<HandPointerTarget> IndexPinchStarted;
    public event Action<HandPointerTarget> MiddlePinchStarted;
    public event Action<HandPointerTarget> RingPinchStarted;
    public event Action<HandPointerTarget> LittlePinchStarted;
    public event Action<HandPointerTarget> IndexDoublePinchStarted;
    public event Action<HandPointerTarget> MiddleDoublePinchStarted;
    public event Action<HandPointerTarget> RingDoublePinchStarted;
    public event Action<HandPointerTarget> LittleDoublePinchStarted;
    public event Action<HandContactTarget> ContactStarted;
    public event Action<HandContactTarget> ContactEnded;

    private readonly Collider[] _contactHits = new Collider[16];
    private static readonly HandLocationStatus RequiredPositionFlags =
        HandLocationStatus.PositionTracked | HandLocationStatus.PositionValid;
    private readonly Vector3[] _contactTipPoints = new Vector3[5];
    private int _contactTipCount;
    private HandPinchDraggableEffect _currentContactDraggableEffect;
    private HandContactTarget _lastContactTarget;
    private readonly bool[] _rawFingerPinching = new bool[FingerTipJoints.Length];
    private readonly bool[] _wasRawFingerPinching = new bool[FingerTipJoints.Length];
    private readonly bool[] _fingerPinching = new bool[FingerTipJoints.Length];
    private readonly bool[] _wasFingerPinching = new bool[FingerTipJoints.Length];
    private readonly float[] _lastRawFingerPinchStartedTime = CreateInitialPinchTimes();
    private readonly float[] _lastFingerPinchStartedTime = CreateInitialPinchTimes();
    private bool _wasRawPinching;
    private bool _wasPinching;
    private float _lastRawPinchStartedTime = float.NegativeInfinity;
    private float _lastPinchStartedTime = float.NegativeInfinity;

    private static float[] CreateInitialPinchTimes()
    {
        var times = new float[FingerTipJoints.Length];
        for (int i = 0; i < times.Length; i++)
        {
            times[i] = float.NegativeInfinity;
        }

        return times;
    }

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

        if (!TryReadHandState(out bool rawIsPinching, out bool isPinching, out Vector3 indexTip, out Vector3 thumbTip))
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
        // Subscribers to pinch events read the current hit immediately.
        UpdatePinchState(rawIsPinching, isPinching);
    }

    private void ResetFrameState()
    {
        IsTracked = false;
        PinchDown = false;
        PinchUp = false;
        RawPinchDown = false;
        RawPinchUp = false;
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

    private bool TryReadHandState(out bool rawIsPinching, out bool isPinching, out Vector3 indexTip, out Vector3 thumbTip)
    {
        rawIsPinching = false;
        isPinching = false;
        indexTip = default;
        thumbTip = default;
        ClearCurrentFingerPinches();

        HandJointLocations joints = new HandJointLocations();
        if (!PXR_HandTracking.GetJointLocations(handType, ref joints))
            return false;

        if (joints.jointLocations == null || joints.jointLocations.Length == 0)
            return false;

        FindClosestPinchFingerTipJoint(joints, out var poseJointPosition);
        UpdateContactTipPoints(joints);

        if (useStrictTrackingValidation &&
            !IsBasePinchPoseValid(joints))
        {
            return false;
        }

        if (!TryGetJointPosition(joints, HandJoint.JointThumbTip, out thumbTip))
        {
            return false;
        }

        indexTip = poseJointPosition;
        if (!TryGetJointRotation(joints, HandJoint.JointWrist, out var wristRotation))
        {
            return false;
        }

        ContactRotation = wristRotation;
        ReadFingerPinches(joints, thumbTip, out rawIsPinching);
        CopyRawFingerPinchesToProtected();
        isPinching = rawIsPinching;

        if (isPinching && !IsPalmUpPinchAllowed(ContactRotation))
        {
            isPinching = false;
            ClearProtectedFingerPinches();
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

    private bool IsBasePinchPoseValid(HandJointLocations joints)
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

        for (int i = 0; i < FingerTipJoints.Length; i++)
        {
            if (HasRequiredPositionStatus(joints, FingerTipJoints[i]))
            {
                return true;
            }
        }

        return false;
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

    private bool TryGetJointPosition(HandJointLocations joints, HandJoint joint, out Vector3 position)
    {
        position = default;
        int index = (int)joint;
        if (index < 0 || index >= joints.jointLocations.Length)
        {
            return false;
        }

        if (useStrictTrackingValidation && !HasRequiredPositionStatus(joints, joint))
        {
            return false;
        }

        position = ToUnityPos(joints.jointLocations[index].pose.Position);
        return true;
    }

    private bool TryGetJointRotation(HandJointLocations joints, HandJoint joint, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        int index = (int)joint;
        if (index < 0 || index >= joints.jointLocations.Length)
        {
            return false;
        }

        if (useStrictTrackingValidation && !HasRequiredPositionStatus(joints, joint))
        {
            return false;
        }

        rotation = ToUnityRot(joints.jointLocations[index].pose.Orientation);
        return true;
    }

    private HandJoint FindClosestPinchFingerTipJoint(HandJointLocations joints, out Vector3 tipPosition)
    {
        if (!TryGetJointPosition(joints, HandJoint.JointThumbTip, out var thumbPosition))
        {
            tipPosition = default;
            return HandJoint.JointIndexTip;
        }

        var bestJoint = HandJoint.JointIndexTip;
        var bestDistance = float.PositiveInfinity;
        tipPosition = default;

        for (int i = 0; i < FingerTipJoints.Length; i++)
        {
            var joint = FingerTipJoints[i];
            if (!TryGetJointPosition(joints, joint, out var candidatePosition))
            {
                continue;
            }

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
            if (!TryGetJointPosition(joints, joint, out var tipPosition))
            {
                continue;
            }

            _contactTipPoints[_contactTipCount++] = tipPosition;
        }
    }

    private void ReadFingerPinches(HandJointLocations joints, Vector3 thumbTip, out bool anyPinching)
    {
        anyPinching = false;
        var releaseThreshold = Mathf.Max(pinchStartDistanceThreshold, pinchReleaseDistanceThreshold);

        for (int i = 0; i < FingerTipJoints.Length; i++)
        {
            var joint = FingerTipJoints[i];
            if (!TryGetJointPosition(joints, joint, out var fingerTip))
            {
                continue;
            }

            var pinchDistance = Vector3.Distance(fingerTip, thumbTip);
            var isFingerPinching = _wasRawFingerPinching[i]
                ? pinchDistance <= releaseThreshold
                : pinchDistance <= pinchStartDistanceThreshold;

            _rawFingerPinching[i] = isFingerPinching;
            anyPinching |= isFingerPinching;
        }
    }

    private void UpdatePinchState(bool rawIsPinching, bool isPinching)
    {
        RawPinchDown = rawIsPinching && !_wasRawPinching;
        RawPinchHeld = rawIsPinching;
        RawPinchUp = !rawIsPinching && _wasRawPinching;

        PinchDown = isPinching && !_wasPinching;
        PinchHeld = isPinching;
        PinchUp = !isPinching && _wasPinching;

        for (int i = 0; i < FingerTipJoints.Length; i++)
        {
            var rawFingerDown = _rawFingerPinching[i] && !_wasRawFingerPinching[i];
            if (rawFingerDown)
            {
                var rawFinger = ToPicoPinchFinger(i);
                RawFingerPinchStarted?.Invoke(rawFinger, CurrentTarget);
                InvokeRawFingerPinchStarted(rawFinger, CurrentTarget);

                if (Time.unscaledTime - _lastRawFingerPinchStartedTime[i] <= doublePinchMaxIntervalSeconds)
                {
                    RawFingerDoublePinchStarted?.Invoke(rawFinger, CurrentTarget);
                    InvokeRawFingerDoublePinchStarted(rawFinger, CurrentTarget);
                    _lastRawFingerPinchStartedTime[i] = float.NegativeInfinity;
                }
                else
                {
                    _lastRawFingerPinchStartedTime[i] = Time.unscaledTime;
                }
            }

            _wasRawFingerPinching[i] = _rawFingerPinching[i];

            var fingerDown = _fingerPinching[i] && !_wasFingerPinching[i];
            if (!fingerDown)
            {
                _wasFingerPinching[i] = _fingerPinching[i];
                continue;
            }

            var finger = ToPicoPinchFinger(i);
            FingerPinchStarted?.Invoke(finger, CurrentTarget);
            InvokeFingerPinchStarted(finger, CurrentTarget);

            if (Time.unscaledTime - _lastFingerPinchStartedTime[i] <= doublePinchMaxIntervalSeconds)
            {
                FingerDoublePinchStarted?.Invoke(finger, CurrentTarget);
                InvokeFingerDoublePinchStarted(finger, CurrentTarget);
                _lastFingerPinchStartedTime[i] = float.NegativeInfinity;
            }
            else
            {
                _lastFingerPinchStartedTime[i] = Time.unscaledTime;
            }

            _wasFingerPinching[i] = _fingerPinching[i];
        }

        if (RawPinchDown)
        {
            RawPinchStarted?.Invoke(CurrentTarget);

            if (Time.unscaledTime - _lastRawPinchStartedTime <= doublePinchMaxIntervalSeconds)
            {
                RawDoublePinchStarted?.Invoke(CurrentTarget);
                _lastRawPinchStartedTime = float.NegativeInfinity;
            }
            else
            {
                _lastRawPinchStartedTime = Time.unscaledTime;
            }
        }

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

        _wasRawPinching = rawIsPinching;
        _wasPinching = isPinching;
    }

    private void ReleasePinchIfNeeded()
    {
        RawPinchHeld = false;
        PinchHeld = false;
        ClearCurrentFingerPinches();

        for (int i = 0; i < _wasRawFingerPinching.Length; i++)
        {
            _wasRawFingerPinching[i] = false;
        }

        for (int i = 0; i < _wasFingerPinching.Length; i++)
        {
            _wasFingerPinching[i] = false;
        }

        if (_wasRawPinching)
        {
            RawPinchUp = true;
            _wasRawPinching = false;
        }

        if (!_wasPinching)
            return;

        PinchUp = true;
        _wasPinching = false;
    }

    private void ClearCurrentFingerPinches()
    {
        for (int i = 0; i < _rawFingerPinching.Length; i++)
        {
            _rawFingerPinching[i] = false;
        }

        for (int i = 0; i < _fingerPinching.Length; i++)
        {
            _fingerPinching[i] = false;
        }
    }

    private void ClearProtectedFingerPinches()
    {
        for (int i = 0; i < _fingerPinching.Length; i++)
        {
            _fingerPinching[i] = false;
        }
    }

    private void CopyRawFingerPinchesToProtected()
    {
        for (int i = 0; i < _fingerPinching.Length; i++)
        {
            _fingerPinching[i] = _rawFingerPinching[i];
        }
    }

    private static PicoPinchFinger ToPicoPinchFinger(int fingerIndex)
    {
        switch (fingerIndex)
        {
            case 0:
                return PicoPinchFinger.Index;
            case 1:
                return PicoPinchFinger.Middle;
            case 2:
                return PicoPinchFinger.Ring;
            default:
                return PicoPinchFinger.Little;
        }
    }

    private void InvokeFingerPinchStarted(PicoPinchFinger finger, HandPointerTarget target)
    {
        switch (finger)
        {
            case PicoPinchFinger.Index:
                IndexPinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Middle:
                MiddlePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Ring:
                RingPinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Little:
                LittlePinchStarted?.Invoke(target);
                break;
        }
    }

    private void InvokeRawFingerPinchStarted(PicoPinchFinger finger, HandPointerTarget target)
    {
        switch (finger)
        {
            case PicoPinchFinger.Index:
                RawIndexPinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Middle:
                RawMiddlePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Ring:
                RawRingPinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Little:
                RawLittlePinchStarted?.Invoke(target);
                break;
        }
    }

    private void InvokeFingerDoublePinchStarted(PicoPinchFinger finger, HandPointerTarget target)
    {
        switch (finger)
        {
            case PicoPinchFinger.Index:
                IndexDoublePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Middle:
                MiddleDoublePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Ring:
                RingDoublePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Little:
                LittleDoublePinchStarted?.Invoke(target);
                break;
        }
    }

    private void InvokeRawFingerDoublePinchStarted(PicoPinchFinger finger, HandPointerTarget target)
    {
        switch (finger)
        {
            case PicoPinchFinger.Index:
                RawIndexDoublePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Middle:
                RawMiddleDoublePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Ring:
                RawRingDoublePinchStarted?.Invoke(target);
                break;
            case PicoPinchFinger.Little:
                RawLittleDoublePinchStarted?.Invoke(target);
                break;
        }
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
