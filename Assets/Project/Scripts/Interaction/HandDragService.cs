using Project.Scripts.Interaction;
using UnityEngine;
using System;

using VContainer;
using VContainer.Unity;

public sealed class HandDragService : ILateTickable, IDisposable
{
    private readonly IPicoHandInput handInput;

    private Transform activeTransform;
    private Rigidbody activeRigidbody;
    private Vector3 initialHandPosition;
    private Quaternion initialHandRotation;
    private Vector3 initialObjectPosition;
    private Quaternion initialObjectRotation;
    private bool hasInitialGrabPose;
    private Vector3 lastContactPosition;
    private Vector3 throwVelocity;
    private bool hasLastContactPosition;
    private float lastPinchHeldTime;
    private RigidbodyInterpolation previousRigidbodyInterpolation = RigidbodyInterpolation.None;
    private bool hasPreviousRigidbodyInterpolation;

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

    public void LateTick()
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
        var targetRotation = handInput.ContactRotation;
        UpdateThrowVelocity(targetPoint);

        if (!hasInitialGrabPose)
        {
            Release();
            return;
        }

        var deltaRotation = targetRotation * Quaternion.Inverse(initialHandRotation);
        var objectRotation = deltaRotation * initialObjectRotation;
        var initialObjectOffsetFromHand = initialObjectPosition - initialHandPosition;
        var targetPosition = targetPoint + deltaRotation * initialObjectOffsetFromHand;

        if (activeRigidbody != null)
        {
            activeRigidbody.position = targetPosition;
            activeRigidbody.rotation = objectRotation;
            return;
        }

        activeTransform.SetPositionAndRotation(targetPosition, objectRotation);
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
        initialHandPosition = grabPoint;
        initialHandRotation = handInput.ContactRotation;
        initialObjectPosition = activeTransform.position;
        initialObjectRotation = activeTransform.rotation;
        hasInitialGrabPose = true;

        if (activeRigidbody != null)
        {
            if (!hasPreviousRigidbodyInterpolation)
            {
                previousRigidbodyInterpolation = activeRigidbody.interpolation;
                hasPreviousRigidbodyInterpolation = true;
            }

            activeRigidbody.interpolation = RigidbodyInterpolation.None;
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
            if (hasPreviousRigidbodyInterpolation)
            {
                activeRigidbody.interpolation = previousRigidbodyInterpolation;
                hasPreviousRigidbodyInterpolation = false;
            }

            activeRigidbody.isKinematic = false;
            activeRigidbody.linearVelocity = throwVelocity;
        }

        activeTransform = null;
        activeRigidbody = null;
        initialHandPosition = default;
        initialHandRotation = Quaternion.identity;
        initialObjectPosition = default;
        initialObjectRotation = Quaternion.identity;
        hasInitialGrabPose = false;
        lastContactPosition = default;
        throwVelocity = default;
        hasLastContactPosition = false;
        lastPinchHeldTime = 0f;
        previousRigidbodyInterpolation = RigidbodyInterpolation.None;
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
