using Project.Scripts.Interaction;
using UnityEngine;
using System;

using VContainer;
using VContainer.Unity;

public sealed class HandDragService : ITickable, IDisposable
{
    private readonly IPicoHandInput handInput;

    private Transform activeTransform;
    private Vector3 localGrabPoint;

    [Inject]
    public HandDragService(IPicoHandInput handInput)
    {
        this.handInput = handInput;

        if (this.handInput != null)
        {
            this.handInput.PinchStarted += HandlePinchStarted;
        }
    }

    public void Tick()
    {
        if (activeTransform == null)
        {
            return;
        }

        if (handInput == null)
        {
            Release();
            return;
        }

        if (!handInput.IsTracked || !handInput.PinchHeld)
        {
            Release();
            return;
        }

        var targetPoint = handInput.ContactPosition;
        var worldGrabOffset = activeTransform.TransformVector(localGrabPoint);
        activeTransform.position = targetPoint - worldGrabOffset;
    }

    private void HandlePinchStarted(HandPointerTarget _)
    {
        if (!handInput.TryGetCurrentContact(out var contactTarget))
        {
            return;
        }

        if (!contactTarget.TryGetComponentInParent<HandPinchDraggableEffect>(out var draggable))
        {
            return;
        }

        activeTransform = draggable.transform;
        localGrabPoint = activeTransform.InverseTransformPoint(contactTarget.Point);
    }

    private void Release()
    {
        activeTransform = null;
        localGrabPoint = default;
    }

    public void Dispose()
    {
        if (handInput != null)
        {
            handInput.PinchStarted -= HandlePinchStarted;
        }

        Release();
    }
}
