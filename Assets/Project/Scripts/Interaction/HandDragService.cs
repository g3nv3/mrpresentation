using UnityEngine;

using VContainer;
using VContainer.Unity;

public sealed class HandDragService : ITickable
{
    private readonly IPicoHandInput handInput;

    private Transform activeTransform;
    private Vector3 localGrabPoint;
    private float grabDistance;

    [Inject]
    public HandDragService(IPicoHandInput handInput)
    {
        this.handInput = handInput;

        if (this.handInput != null)
        {
            this.handInput.PinchStarted += HandlePinchStarted;
            this.handInput.PinchEnded += HandlePinchEnded;
        }
    }

    public void Tick()
    {
        if (activeTransform == null)
        {
            return;
        }

        if (handInput == null || !handInput.IsTracked || !handInput.PinchHeld)
        {
            Release();
            return;
        }

        var ray = handInput.AimRay;
        var targetPoint = ray.origin + ray.direction * grabDistance;
        var worldGrabOffset = activeTransform.TransformVector(localGrabPoint);
        activeTransform.position = targetPoint - worldGrabOffset;
    }

    private void HandlePinchStarted(HandPointerTarget target)
    {
        if (!target.TryGetComponentInParent<HandPinchDraggable>(out var draggable))
        {
            return;
        }

        activeTransform = draggable.transform;
        localGrabPoint = activeTransform.InverseTransformPoint(target.Hit.point);
        grabDistance = Mathf.Max(0.05f, Vector3.Dot(target.Hit.point - target.AimRay.origin, target.AimRay.direction));
    }

    private void HandlePinchEnded(HandPointerTarget _)
    {
        Release();
    }

    private void Release()
    {
        activeTransform = null;
        localGrabPoint = default;
        grabDistance = 0f;
    }
}
