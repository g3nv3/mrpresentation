using System.Collections.Generic;

using UnityEngine;

using VContainer;
using VContainer.Unity;

public sealed class QrMarkerPlacementService : IQrMarkerPlacementService
{
    private readonly IQrMarkerPayloadParser payloadParser;
    private readonly IQrMarkerRegistry markerRegistry;
    private readonly IQrPoseResolver poseResolver;
    private readonly IObjectResolver objectResolver;

    private readonly Dictionary<string, GameObject> spawnedByMarkerId = new Dictionary<string, GameObject>();

    [Inject]
    public QrMarkerPlacementService(
        IQrMarkerPayloadParser payloadParser,
        IQrMarkerRegistry markerRegistry,
        IQrPoseResolver poseResolver,
        IObjectResolver objectResolver)
    {
        this.payloadParser = payloadParser;
        this.markerRegistry = markerRegistry;
        this.poseResolver = poseResolver;
        this.objectResolver = objectResolver;
    }

    public bool TryPlaceOrUpdate(in QrDetection detection)
    {
        if (!payloadParser.TryParse(detection.Text, out var payload))
        {
            return false;
        }

        if (!markerRegistry.TryGet(payload.MarkerId, out var definition) || definition.Prefab == null)
        {
            return false;
        }

        if (!poseResolver.TryResolvePose(detection, out var markerPose))
        {
            return false;
        }

        var rotation = markerPose.rotation * Quaternion.Euler(definition.RotationOffset);
        var position = markerPose.position + markerPose.rotation * definition.PositionOffset;

        if (!spawnedByMarkerId.TryGetValue(payload.MarkerId, out var instance) || instance == null)
        {
            instance = objectResolver.Instantiate(definition.Prefab, position, rotation);
            spawnedByMarkerId[payload.MarkerId] = instance;
            return true;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        return true;
    }
}
