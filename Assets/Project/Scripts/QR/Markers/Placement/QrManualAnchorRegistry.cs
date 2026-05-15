using System;
using System.Collections.Generic;

using Project.Scripts.Interaction;
using UnityEngine;

public sealed class QrManualAnchorRegistry : IDisposable
{
    private readonly QrFactory qrFactory;
    private readonly IQrMarkerRegistry markerRegistry;
    private readonly GameObject btnPrefab;
    private const int RequiredSampleCount = 10;
    private const float MaxPositionSpreadMeters = 0.008f;
    private const float MaxRotationSpreadDegrees = 10f;

    private readonly Dictionary<string, AnchorState> stateByMarkerId =
        new Dictionary<string, AnchorState>(StringComparer.Ordinal);

    public QrManualAnchorRegistry(
        QrManualAnchorVisualOptions visualOptions,
        QrFactory qrFactory,
        IQrMarkerRegistry markerRegistry)
    {
        this.qrFactory = qrFactory;
        this.markerRegistry = markerRegistry;
        btnPrefab = visualOptions.AnchorPrefab;
    }

    public bool TryGetLockedAnchor(string markerId, out Pose pose)
    {
        pose = default;
        if (string.IsNullOrWhiteSpace(markerId) ||
            !stateByMarkerId.TryGetValue(markerId, out var state))
        {
            return false;
        }

        if (!state.HasLockedAnchor)
        {
            return false;
        }

        if (state.AnchorObject == null)
        {
            stateByMarkerId.Remove(markerId);
            return false;
        }

        pose = state.LockedPose;
        return true;
    }

    public AnchorObservationResult RegisterObservation(string markerId, in Pose pose)
    {
        if (string.IsNullOrWhiteSpace(markerId))
        {
            return AnchorObservationResult.Invalid;
        }

        if (!stateByMarkerId.TryGetValue(markerId, out var state))
        {
            state = new AnchorState();
            stateByMarkerId.Add(markerId, state);
        }

        if (state.HasLockedAnchor)
        {
            if (state.AnchorObject == null)
            {
                stateByMarkerId.Remove(markerId);
                return RegisterObservation(markerId, pose);
            }

            return AnchorObservationResult.AlreadyAnchored(RequiredSampleCount, state.LockedPose);
        }

        state.AddSample(pose, RequiredSampleCount);
        var averagedPose = state.ComputeAveragePose();
        var metrics = state.ComputeSpreadMetrics(averagedPose);
        if (state.SampleCount < RequiredSampleCount)
        {
            return AnchorObservationResult.Collecting(
                state.SampleCount,
                RequiredSampleCount,
                averagedPose,
                metrics.MaxPositionSpreadMeters,
                metrics.MaxRotationSpreadDegrees);
        }

        if (metrics.MaxPositionSpreadMeters > MaxPositionSpreadMeters ||
            metrics.MaxRotationSpreadDegrees > MaxRotationSpreadDegrees)
        {
            return AnchorObservationResult.Unstable(
                state.SampleCount,
                RequiredSampleCount,
                averagedPose,
                metrics.MaxPositionSpreadMeters,
                metrics.MaxRotationSpreadDegrees);
        }

        var anchorObject = CreateAnchorObject(markerId, averagedPose);
        if (anchorObject == null)
        {
            return AnchorObservationResult.Invalid;
        }

        state.Lock(averagedPose, anchorObject);
        return AnchorObservationResult.Created(
            RequiredSampleCount,
            averagedPose,
            metrics.MaxPositionSpreadMeters,
            metrics.MaxRotationSpreadDegrees);
    }

    public void Dispose()
    {
        foreach (var entry in stateByMarkerId)
        {
            if (entry.Value.AnchorObject != null)
            {
                UnityEngine.Object.Destroy(entry.Value.AnchorObject);
            }
        }

        stateByMarkerId.Clear();
    }

    private GameObject CreateAnchorObject(string markerId, in Pose pose)
    {
        if (markerRegistry != null &&
            markerRegistry.TryGet(markerId, out var definition) &&
            definition != null &&
            definition.MarkerSpawnMode == QrMarkerDefinition.SpawnMode.DirectObject)
        {
            return qrFactory.TryCreateMarker(markerId, pose, out var objectInstance)
                ? objectInstance
                : null;
        }

        if (btnPrefab == null)
        {
            return null;
        }

        qrFactory.CreateButton(btnPrefab, markerId, pose, out var instance);
        return instance != null ? instance.gameObject : null;
    }

    private sealed class AnchorState
    {
        private readonly List<Pose> samples = new List<Pose>(RequiredSampleCount);

        public int SampleCount => samples.Count;
        public bool HasLockedAnchor { get; private set; }
        public Pose LockedPose { get; private set; }
        public GameObject AnchorObject { get; private set; }

        public void AddSample(in Pose pose, int maxSampleCount)
        {
            if (samples.Count == maxSampleCount)
            {
                samples.RemoveAt(0);
            }

            samples.Add(pose);
        }

        public Pose ComputeAveragePose()
        {
            if (samples.Count == 0)
            {
                return default;
            }

            var averagePosition = Vector3.zero;
            for (var i = 0; i < samples.Count; i++)
            {
                averagePosition += samples[i].position;
            }

            averagePosition /= samples.Count;

            var firstRotation = samples[0].rotation;
            var accumulated = Vector4.zero;
            for (var i = 0; i < samples.Count; i++)
            {
                var rotation = samples[i].rotation;
                if (Quaternion.Dot(rotation, firstRotation) < 0f)
                {
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                }

                accumulated += new Vector4(rotation.x, rotation.y, rotation.z, rotation.w);
            }

            var averagedRotation = new Quaternion(
                accumulated.x / samples.Count,
                accumulated.y / samples.Count,
                accumulated.z / samples.Count,
                accumulated.w / samples.Count).normalized;

            if (averagedRotation == Quaternion.identity && Quaternion.Dot(firstRotation, Quaternion.identity) < 0.999f)
            {
                averagedRotation = firstRotation;
            }

            return new Pose(averagePosition, averagedRotation);
        }

        public SpreadMetrics ComputeSpreadMetrics(in Pose averagePose)
        {
            var maxPositionSpreadMeters = 0f;
            var maxRotationSpreadDegrees = 0f;
            for (var i = 0; i < samples.Count; i++)
            {
                maxPositionSpreadMeters = Mathf.Max(
                    maxPositionSpreadMeters,
                    Vector3.Distance(samples[i].position, averagePose.position));
                maxRotationSpreadDegrees = Mathf.Max(
                    maxRotationSpreadDegrees,
                    Quaternion.Angle(samples[i].rotation, averagePose.rotation));
            }

            return new SpreadMetrics(maxPositionSpreadMeters, maxRotationSpreadDegrees);
        }

        public void Lock(in Pose pose, GameObject anchorObject)
        {
            HasLockedAnchor = true;
            LockedPose = pose;
            AnchorObject = anchorObject;
            samples.Clear();
        }
    }

    private readonly struct SpreadMetrics
    {
        public readonly float MaxPositionSpreadMeters;
        public readonly float MaxRotationSpreadDegrees;

        public SpreadMetrics(float maxPositionSpreadMeters, float maxRotationSpreadDegrees)
        {
            MaxPositionSpreadMeters = maxPositionSpreadMeters;
            MaxRotationSpreadDegrees = maxRotationSpreadDegrees;
        }
    }
}
