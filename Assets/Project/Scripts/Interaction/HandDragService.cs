using Project.Scripts.Interaction;
using UnityEngine;
using System;

using VContainer;
using VContainer.Unity;

public sealed class HandDragService : ITickable, IDisposable
{
    private readonly IPicoHandInput handInput;

    private Transform activeTransform;
    private Rigidbody activeRigidbody;
    private Vector3 localGrabPoint;
    private Vector3 lastContactPosition;
    private Vector3 throwVelocity;
    private bool hasLastContactPosition;
    private float lastPinchHeldTime;

    private const float ThrowVelocitySmoothing = 0.45f;
    private const float PinchLossGraceSeconds = 0.08f;

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

        if (handInput.IsTracked && handInput.PinchHeld)
        {
            lastPinchHeldTime = Time.time;
        }
        else if (Time.time - lastPinchHeldTime > PinchLossGraceSeconds)
        {
            Release();
            return;
        }
        else
        {
            return;
        }

        var targetPoint = handInput.ContactPosition;
        UpdateThrowVelocity(targetPoint);

        var worldGrabOffset = activeTransform.TransformVector(localGrabPoint);
        var targetPosition = targetPoint - worldGrabOffset;

        if (activeRigidbody != null)
        {
            activeRigidbody.position = targetPosition;
            return;
        }

        activeTransform.position = targetPosition;
    }

    private void HandlePinchStarted(HandPointerTarget _)
    {
        HandPinchDraggableEffect draggable;
        Vector3 grabPoint;
        if (handInput.TryGetCurrentContact(out var contactTarget) &&
            contactTarget.TryGetComponentInParent<HandPinchDraggableEffect>(out draggable))
        {
            grabPoint = contactTarget.Point;
        }
        else if (_.TryGetComponentInParent<HandPinchDraggableEffect>(out draggable))
        {
            grabPoint = _.Hit.point;
        }
        else
        {
            return;
        }

        var draggableTransform = draggable.transform;
        activeRigidbody = draggableTransform.GetComponentInParent<Rigidbody>();
        activeTransform = activeRigidbody != null ? activeRigidbody.transform : draggableTransform;
        localGrabPoint = activeTransform.InverseTransformPoint(grabPoint);

        if (activeRigidbody != null)
        {
            activeRigidbody.isKinematic = true;
        }

        throwVelocity = Vector3.zero;
        lastContactPosition = handInput.ContactPosition;
        hasLastContactPosition = true;
        lastPinchHeldTime = Time.time;
    }

    private void Release()
    {
        if (activeRigidbody != null)
        {
            activeRigidbody.isKinematic = false;
            activeRigidbody.linearVelocity = throwVelocity;
        }

        activeTransform = null;
        activeRigidbody = null;
        localGrabPoint = default;
        lastContactPosition = default;
        throwVelocity = default;
        hasLastContactPosition = false;
        lastPinchHeldTime = 0f;
    }

    private void UpdateThrowVelocity(Vector3 currentContactPosition)
    {
        if (!hasLastContactPosition)
        {
            lastContactPosition = currentContactPosition;
            hasLastContactPosition = true;
            throwVelocity = Vector3.zero;
            return;
        }

        var deltaTime = Time.deltaTime;
        if (deltaTime <= Mathf.Epsilon)
        {
            return;
        }

        var frameVelocity = (currentContactPosition - lastContactPosition) / deltaTime;
        throwVelocity = Vector3.Lerp(throwVelocity, frameVelocity, ThrowVelocitySmoothing);
        lastContactPosition = currentContactPosition;
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
