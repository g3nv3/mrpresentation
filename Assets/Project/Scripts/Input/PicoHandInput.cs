using System;
using UnityEngine;
using Unity.XR.PXR;

public interface IPicoHandInput
{
    bool IsTracked { get; }
    bool PinchDown { get; }
    bool PinchHeld { get; }
    bool PinchUp { get; }
    Ray AimRay { get; }
    bool HasRaycastHit { get; }
    RaycastHit RaycastHit { get; }

    event Action PinchStarted;
    event Action PinchEnded;
}

public class PicoHandInput : MonoBehaviour, IPicoHandInput
{
    [SerializeField] private HandType handType = HandType.HandRight;
    [SerializeField] private float pinchDistanceThreshold = 0.013f;
    [SerializeField] private float rayDistance = 20f;
    [SerializeField] private LayerMask rayMask;
    [SerializeField] private Transform sphere;

    public bool IsTracked { get; private set; }
    public bool PinchDown { get; private set; }
    public bool PinchHeld { get; private set; }
    public bool PinchUp { get; private set; }
    public Ray AimRay { get; private set; }
    public bool HasRaycastHit { get; private set; }
    public RaycastHit RaycastHit { get; private set; }

    public event Action PinchStarted;
    public event Action PinchEnded;

    private bool _wasPinching;

    private void Update()
    {
        ResetFrameState();

        if (!TryReadHandState(out bool isPinching, out Ray ray))
        {
            ReleasePinchIfNeeded();
            return;
        }

        IsTracked = true;
        AimRay = ray;

        UpdateRaycast(ray);
        // Subscribers to PinchStarted read the current hit immediately.
        UpdatePinchState(isPinching);
    }

    private void ResetFrameState()
    {
        IsTracked = false;
        PinchDown = false;
        PinchUp = false;
        HasRaycastHit = false;
        RaycastHit = default;
    }

    private bool TryReadHandState(out bool isPinching, out Ray ray)
    {
        isPinching = false;
        ray = default;

        HandJointLocations joints = new HandJointLocations();
        bool ok = PXR_HandTracking.GetJointLocations(handType, ref joints);
        if (!ok || joints.jointLocations == null || joints.jointLocations.Length == 0)
            return false;

        Vector3 indexTip = ToUnityPos(joints.jointLocations[(int)HandJoint.JointIndexTip].pose.Position);
        Vector3 thumbTip = ToUnityPos(joints.jointLocations[(int)HandJoint.JointThumbTip].pose.Position);
        isPinching = Vector3.Distance(indexTip, thumbTip) < pinchDistanceThreshold;

        HandAimState aimState = new HandAimState();
        ok = PXR_HandTracking.GetAimState(handType, ref aimState);
        if (!ok)
            return false;

        Vector3 rayOrigin = ToUnityPos(aimState.aimRayPose.Position);
        Quaternion rayRotation = ToUnityRot(aimState.aimRayPose.Orientation);
        Vector3 rayDirection = (rayRotation * Vector3.forward).normalized;
        rayOrigin += rayDirection * 0.02f;
        ray = new Ray(rayOrigin, rayDirection);

        return true;
    }

    private void UpdatePinchState(bool isPinching)
    {
        PinchDown = isPinching && !_wasPinching;
        PinchHeld = isPinching;
        PinchUp = !isPinching && _wasPinching;

        if (PinchDown)
            PinchStarted?.Invoke();

        if (PinchUp)
            PinchEnded?.Invoke();

        _wasPinching = isPinching;
    }

    private void ReleasePinchIfNeeded()
    {
        PinchHeld = false;

        if (!_wasPinching)
            return;

        PinchUp = true;
        _wasPinching = false;
        PinchEnded?.Invoke();
    }

    private void UpdateRaycast(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, rayMask))
            return;

        HasRaycastHit = true;
        RaycastHit = hit;

        if (sphere != null)
            sphere.position = hit.point;
    }

    private Vector3 ToUnityPos(Vector3f p)
    {
        return new Vector3(p.x, p.y, -p.z);
    }

    private Quaternion ToUnityRot(Quatf q)
    {
        return new Quaternion(-q.x, -q.y, q.z, q.w);
    }
}
