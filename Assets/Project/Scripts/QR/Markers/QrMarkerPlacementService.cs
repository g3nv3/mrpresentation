using System;
using System.Collections.Generic;

using UnityEngine;

using VContainer;
using VContainer.Unity;

public sealed class QrMarkerPlacementService : IQrMarkerPlacementService, IDisposable
{
    private const string CreatePrompt = "QR найден. Нажмите pinch, чтобы создать";
    private const string DeletePrompt = "Модель уже создана. Нажмите pinch, чтобы удалить";
    private const string SurfacePrompt = "QR найден, но поверхность для размещения не определена";
    private const bool KeepLastPlacementPoseOnResolveFailure = true;

    private readonly IQrMarkerPayloadParser payloadParser;
    private readonly IQrMarkerRegistry markerRegistry;
    private readonly IQrPoseResolver poseResolver;
    private readonly IObjectResolver objectResolver;
    private readonly IPicoHandInput handInput;

    private readonly Dictionary<string, SpawnedMarkerState> spawnedByMarkerId =
        new Dictionary<string, SpawnedMarkerState>(StringComparer.Ordinal);

    private string currentMarkerId;
    private Pose currentPlacementPose;
    private bool hasCurrentPlacementPose;

    [Inject]
    public QrMarkerPlacementService(
        IQrMarkerPayloadParser payloadParser,
        IQrMarkerRegistry markerRegistry,
        IQrPoseResolver poseResolver,
        IObjectResolver objectResolver,
        IPicoHandInput handInput)
    {
        this.payloadParser = payloadParser;
        this.markerRegistry = markerRegistry;
        this.poseResolver = poseResolver;
        this.objectResolver = objectResolver;
        this.handInput = handInput;

        if (this.handInput != null)
        {
            this.handInput.PinchStarted += HandlePinchStarted;
        }
    }

    public bool TryProcessDetection(in QrDetection detection, out string statusText)
    {
        statusText = null;

        if (!payloadParser.TryParse(detection.Text, out var payload))
        {
            ClearCurrentDetection();
            statusText = "QR найден, но формат не поддерживается";
            return false;
        }

        if (!markerRegistry.TryGet(payload.MarkerId, out var definition) || definition.Prefab == null)
        {
            ClearCurrentDetection();
            statusText = $"QR \"{payload.MarkerId}\" не зарегистрирован";
            return false;
        }

        var isSameMarker = string.Equals(currentMarkerId, payload.MarkerId, StringComparison.Ordinal);
        if (!isSameMarker)
        {
            poseResolver?.ClearResolvedPose();
            currentMarkerId = payload.MarkerId;
            currentPlacementPose = default;
            hasCurrentPlacementPose = false;
        }

        TryUpdateCurrentPlacementPose(definition, detection);

        if (IsSpawned(payload.MarkerId))
        {
            statusText = hasCurrentPlacementPose ? BuildStatusText(DeletePrompt) : BuildSurfaceStatusText();
            return true;
        }

        statusText = hasCurrentPlacementPose ? BuildStatusText(CreatePrompt) : BuildSurfaceStatusText();
        return true;
    }

    public void ClearCurrentDetection()
    {
        currentMarkerId = null;
        currentPlacementPose = default;
        hasCurrentPlacementPose = false;
        poseResolver?.ClearResolvedPose();
    }

    public void Dispose()
    {
        if (handInput != null)
        {
            handInput.PinchStarted -= HandlePinchStarted;
        }
    }

    private void HandlePinchStarted(HandPointerTarget target)
    {
        if (string.IsNullOrEmpty(currentMarkerId))
        {
            return;
        }

        if (handInput.TryGetCurrentContact(out var contactTarget) &&
            contactTarget.TryGetComponentInParent<HandPinchDraggable>(out _))
        {
            return;
        }

        if (target.TryGetComponentInParent<HandPinchDraggable>(out _))
        {
            return;
        }

        if (IsSpawned(currentMarkerId))
        {
            DeleteMarker(currentMarkerId);
            return;
        }

        if (!markerRegistry.TryGet(currentMarkerId, out var definition) || definition.Prefab == null)
        {
            return;
        }

        if (!hasCurrentPlacementPose)
        {
            return;
        }

        CreateMarker(currentMarkerId, definition, currentPlacementPose);
    }

    private void TryUpdateCurrentPlacementPose(QrMarkerDefinition definition, in QrDetection detection)
    {
        if (!TryBuildPlacementPose(definition, detection, out var placementPose))
        {
            if (KeepLastPlacementPoseOnResolveFailure && hasCurrentPlacementPose)
            {
                return;
            }

            currentPlacementPose = default;
            hasCurrentPlacementPose = false;
            return;
        }

        currentPlacementPose = placementPose;
        hasCurrentPlacementPose = true;
    }

    private bool TryBuildPlacementPose(QrMarkerDefinition definition, in QrDetection detection, out Pose placementPose)
    {
        placementPose = default;

        if (!poseResolver.TryResolvePose(detection, definition, out var markerPose))
        {
            return false;
        }

        var rotation = markerPose.rotation * Quaternion.Euler(definition.RotationOffset);
        var position = markerPose.position + markerPose.rotation * definition.PositionOffset;
        placementPose = new Pose(position, rotation);
        return true;
    }

    private void CreateMarker(string markerId, QrMarkerDefinition definition, in Pose placementPose)
    {
        var instance = objectResolver.Instantiate(definition.Prefab, placementPose.position, placementPose.rotation);
        if (instance.GetComponent<HandPinchDraggable>() == null)
        {
            instance.AddComponent<HandPinchDraggable>();
        }

        spawnedByMarkerId[markerId] = new SpawnedMarkerState(instance);
    }

    private void DeleteMarker(string markerId)
    {
        if (!spawnedByMarkerId.TryGetValue(markerId, out var state))
        {
            return;
        }

        if (state.Instance != null)
        {
            UnityEngine.Object.Destroy(state.Instance);
        }

        spawnedByMarkerId.Remove(markerId);
    }

    private bool IsSpawned(string markerId)
    {
        return spawnedByMarkerId.TryGetValue(markerId, out var state) && state.Instance != null;
    }

    private string BuildSurfaceStatusText()
    {
        return BuildDebugStatusText(SurfacePrompt);
    }

    private string BuildStatusText(string prompt)
    {
        return BuildDebugStatusText(prompt);
    }

    private string BuildDebugStatusText(string prompt)
    {
        var statusText = prompt;

        if (hasCurrentPlacementPose)
        {
            statusText += "\n" +
                          $"spawn={FormatVector3(currentPlacementPose.position)}\n" +
                          $"spawnEuler={FormatVector3(currentPlacementPose.rotation.eulerAngles)}";
        }

        if (poseResolver == null || string.IsNullOrWhiteSpace(poseResolver.LastDebugStatus))
        {
            return statusText;
        }

        return $"{statusText}\n{poseResolver.LastDebugStatus}";
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }

    private readonly struct SpawnedMarkerState
    {
        public readonly GameObject Instance;

        public SpawnedMarkerState(GameObject instance)
        {
            Instance = instance;
        }
    }
}
