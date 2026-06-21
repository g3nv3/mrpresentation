using UnityEngine;

namespace Project.Scripts
{
    public sealed class QrMarkerAutoAnchorService : IQrMarkerPlacementService
    {
        private const string UnsupportedPayloadPrompt = "QR найден, но формат не поддерживается";
        private const string UnregisteredPrompt = "QR не зарегистрирован";
        private const string ResolveFailedPrompt = "QR найден, но центр не определен";
        private const string TrackingPrompt = "QR трекается: ";

        private readonly IQrMarkerPayloadParser payloadParser;
        private readonly IQrMarkerRegistry markerRegistry;
        private readonly IQrPoseResolver poseResolver;
        private readonly QrManualAnchorRegistry manualAnchorRegistry;
        private readonly QrManualAnchorOptions manualAnchorOptions;

        public QrMarkerAutoAnchorService(
            IQrMarkerPayloadParser payloadParser,
            IQrMarkerRegistry markerRegistry,
            IQrPoseResolver poseResolver,
            QrManualAnchorRegistry manualAnchorRegistry,
            QrManualAnchorOptions manualAnchorOptions)
        {
            this.payloadParser = payloadParser;
            this.markerRegistry = markerRegistry;
            this.poseResolver = poseResolver;
            this.manualAnchorRegistry = manualAnchorRegistry;
            this.manualAnchorOptions = manualAnchorOptions;
        }

        public bool TryProcessDetection(in QrDetection detection, out string statusText)
        {
            statusText = null;

            if (!payloadParser.TryParse(detection.Text, out var payload))
            {
                poseResolver?.SetScanProgress(0f, false);
                statusText = BuildStatusText(UnsupportedPayloadPrompt);
                poseResolver?.ClearResolvedPose();
                return false;
            }

            if (!markerRegistry.TryGet(payload.MarkerId, out var definition))
            {
                poseResolver?.SetScanProgress(0f, false);
                statusText = BuildStatusText($"{UnregisteredPrompt}: \"{payload.MarkerId}\"");
                poseResolver?.ClearResolvedPose();
                return false;
            }

            if (!manualAnchorOptions.CreateAnchors)
            {
                if (!poseResolver.TryResolvePose(detection, definition, out var trackingPose))
                {
                    poseResolver?.SetScanProgress(0f, false);
                    statusText = BuildStatusText(ResolveFailedPrompt, null, null);
                    return false;
                }

                poseResolver.SetScanProgress(0f, false);
                statusText = BuildStatusText(
                    $"{TrackingPrompt}{payload.MarkerId}",
                    trackingPose,
                    null,
                    "track");
                return true;
            }

            if (manualAnchorRegistry.TryGetLockedAnchor(payload.MarkerId, out var lockedPose))
            {
                poseResolver?.SetScanProgress(0f, false);
                poseResolver?.ClearResolvedPose();
                statusText = BuildStatusText(
                    $"QR anchor зафиксирован: {payload.MarkerId}",
                    lockedPose,
                    null);
                return true;
            }

            if (!poseResolver.TryResolvePose(detection, definition, out var markerPose))
            {
                poseResolver?.SetScanProgress(0f, false);
                statusText = BuildStatusText(ResolveFailedPrompt, null, null);
                return false;
            }

            var observation = manualAnchorRegistry.RegisterObservation(payload.MarkerId, markerPose);
            poseResolver.SetScanProgress(
                observation.NormalizedProgress,
                observation.State == AnchorObservationResult.ObservationState.Collecting ||
                observation.State == AnchorObservationResult.ObservationState.Unstable ||
                observation.State == AnchorObservationResult.ObservationState.Created);
            statusText = observation.State switch
            {
                AnchorObservationResult.ObservationState.Collecting => BuildStatusText(
                    $"Стабилизация QR anchor {payload.MarkerId}: {observation.SampleCount}/{observation.RequiredSampleCount}",
                    observation.Pose,
                    observation),
                AnchorObservationResult.ObservationState.Unstable => BuildStatusText(
                    $"QR anchor нестабилен {payload.MarkerId}: {observation.SampleCount}/{observation.RequiredSampleCount}",
                    observation.Pose,
                    observation),
                AnchorObservationResult.ObservationState.Created => BuildStatusText(
                    $"QR anchor создан: {payload.MarkerId}",
                    observation.Pose,
                    observation),
                AnchorObservationResult.ObservationState.AlreadyAnchored => BuildStatusText(
                    $"QR anchor уже зафиксирован: {payload.MarkerId}",
                    observation.Pose,
                    observation),
                _ => BuildStatusText(ResolveFailedPrompt, null, null)
            };
            return observation.State != AnchorObservationResult.ObservationState.Invalid;
        }

        public void ClearCurrentDetection()
        {
            poseResolver?.ClearResolvedPose();
        }

        private string BuildStatusText(
            string prompt,
            Pose? anchorPose = null,
            AnchorObservationResult? observation = null,
            string poseLabel = "anchor")
        {
            var statusText = prompt;
            if (anchorPose.HasValue)
            {
                statusText += "\n" +
                              $"{poseLabel}={FormatVector3(anchorPose.Value.position)}\n" +
                              $"{poseLabel}Euler={FormatVector3(anchorPose.Value.rotation.eulerAngles)}";
            }

            if (observation.HasValue &&
                (observation.Value.State == AnchorObservationResult.ObservationState.Collecting ||
                 observation.Value.State == AnchorObservationResult.ObservationState.Unstable ||
                 observation.Value.State == AnchorObservationResult.ObservationState.Created))
            {
                statusText += "\n" +
                              $"spread={observation.Value.MaxPositionSpreadMeters:F3}m rot={observation.Value.MaxRotationSpreadDegrees:F1}";
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
    }
}
