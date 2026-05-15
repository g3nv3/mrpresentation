using Unity.XR.PXR;
using UnityEngine;

public sealed partial class QrCameraRayPoseResolver
{
    private string BuildDebugStatus(
        string outcome,
        in QrDetection detection,
        CameraProjectionData? context,
        Vector2? adjustedImagePoint,
        Ray? ray,
        RaycastHit? hit,
        Vector3? resolvedPosition)
    {
        var mode = context.HasValue && !string.IsNullOrWhiteSpace(context.Value.Mode)
            ? context.Value.Mode
            : UnavailableValue;
        var note = context.HasValue && !string.IsNullOrWhiteSpace(context.Value.Note)
            ? context.Value.Note
            : UnavailableValue;
        var intrinsicsText = context.HasValue
            ? FormatIntrinsics(context.Value.Intrinsics)
            : UnavailableValue;
        var projectionText = context.HasValue &&
                             TryBuildProjectionModel(
                                 context.Value.Intrinsics,
                                 detection.FrameWidth,
                                 detection.FrameHeight,
                                 out var projectionModel)
            ? FormatProjectionModel(projectionModel)
            : UnavailableValue;
        var headPoseText = context.HasValue
            ? FormatPose(context.Value.HeadWorldPose)
            : UnavailableValue;
        var trackingOriginText = context.HasValue
            ? FormatPose(context.Value.TrackingOriginWorldPose)
            : UnavailableValue;
        var currentHeadPoseText = context.HasValue
            ? FormatPose(context.Value.CurrentHeadWorldPose)
            : UnavailableValue;
        var headDeltaText = context.HasValue
            ? FormatPoseDelta(context.Value.CurrentHeadWorldPose, context.Value.HeadWorldPose)
            : UnavailableValue;
        var rawCameraLocalPoseText = context.HasValue
            ? FormatPose(context.Value.RawCameraLocalPose)
            : UnavailableValue;
        var cameraLocalPoseText = context.HasValue
            ? FormatPose(context.Value.CameraLocalPose)
            : UnavailableValue;
        var cameraWorldPoseText = context.HasValue
            ? FormatPose(context.Value.CameraWorldPose)
            : UnavailableValue;
        var finderText = BuildFinderDebugText(detection);

        return outcome + "\n" +
               $"mode={mode}\n" +
               $"frame={FormatFrameSize(detection.FrameWidth, detection.FrameHeight)}\n" +
               $"centerMirrorX={(mirrorImageX ? "1" : "0")} pointsMirrorX={(GetResultPointsMirrorX() ? "1" : "0")}\n" +
               $"forwardZ={(invertCameraSpaceForward ? "-1" : "+1")}\n" +
               $"extrinsics={cameraExtrinsicsInterpretation}\n" +
               $"projection={projectionModelMode}\n" +
               $"sdk={intrinsicsText}\n" +
               $"proj={projectionText}\n" +
               $"qrBasis={(orientUsingQrResultPoints ? "finder" : "head")}\n" +
               $"raw={FormatVector2(detection.ImageCenter)} adjusted={FormatNullableVector2(adjustedImagePoint)}\n" +
               $"finder={finderText}\n" +
               $"tracking={trackingOriginText}\n" +
               $"deviceUsed={headPoseText}\n" +
               $"deviceCurrent={currentHeadPoseText}\n" +
               $"deviceDelta={headDeltaText}\n" +
               $"motion=lin{(context.HasValue ? context.Value.CurrentDeviceLinearSpeedMetersPerSecond : 0f):F3}/ang{(context.HasValue ? context.Value.CurrentDeviceAngularSpeedDegreesPerSecond : 0f):F1} filter=pos{lastPositionBlend:F2}/rot{lastRotationBlend:F2}\n" +
               $"camLocalRaw={rawCameraLocalPoseText}\n" +
               $"camLocal={cameraLocalPoseText}\n" +
               $"camAdjust=t{(ignoreReportedCameraTranslation ? "0" : "1")} r{(ignoreReportedCameraRotation ? "0" : "1")} z{(flipReportedCameraTranslationZ ? "-1" : "+1")}\n" +
               $"camWorld={cameraWorldPoseText}\n" +
               $"origin={FormatNullableRayOrigin(ray)}\n" +
               $"dir={FormatNullableRayDirection(ray)}\n" +
               $"hit={FormatNullableHitPoint(hit)} normal={FormatNullableHitNormal(hit)}\n" +
               $"resolved={FormatNullableVector3(resolvedPosition)}\n" +
               $"note={note}";
    }

    private string BuildFinderDebugText(in QrDetection detection)
    {
        if (!TryGetOrderedFinderPoints(
                detection.ImageResultPoints,
                out var bottomLeftImage,
                out var topLeftImage,
                out var topRightImage))
        {
            return "none";
        }

        var bottomLeftAdjusted = TryAdjustImagePoint(
            bottomLeftImage,
            detection.FrameWidth,
            detection.FrameHeight,
            GetResultPointsMirrorX(),
            out var adjustedBottomLeft)
            ? FormatVector2(adjustedBottomLeft)
            : UnavailableValue;
        var topLeftAdjusted = TryAdjustImagePoint(
            topLeftImage,
            detection.FrameWidth,
            detection.FrameHeight,
            GetResultPointsMirrorX(),
            out var adjustedTopLeft)
            ? FormatVector2(adjustedTopLeft)
            : UnavailableValue;
        var topRightAdjusted = TryAdjustImagePoint(
            topRightImage,
            detection.FrameWidth,
            detection.FrameHeight,
            GetResultPointsMirrorX(),
            out var adjustedTopRight)
            ? FormatVector2(adjustedTopRight)
            : UnavailableValue;

        return
            $"bl={FormatVector2(bottomLeftImage)}>{bottomLeftAdjusted} tl={FormatVector2(topLeftImage)}>{topLeftAdjusted} tr={FormatVector2(topRightImage)}>{topRightAdjusted}";
    }

    private void MaybeLog(string outcome, in QrDetection detection, in CameraProjectionData context, in Ray ray)
    {
        if (!verbosePoseDiagnostics || Time.unscaledTime < nextPoseDiagnosticsTime)
        {
            return;
        }

        nextPoseDiagnosticsTime = Time.unscaledTime + Mathf.Max(0.1f, poseDiagnosticsInterval);
        Debug.Log(
            $"[QRRAY] outcome={outcome} mode={context.Mode} capture={detection.CaptureTime} image={detection.ImageId} center={FormatVector2(detection.ImageCenter)} origin={FormatVector3(ray.origin)} dir={FormatVector3(ray.direction.normalized)} note={context.Note}",
            this);
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:F1},{value.y:F1})";
    }

    private static string FormatNullableVector2(Vector2? value)
    {
        return value.HasValue ? FormatVector2(value.Value) : UnavailableValue;
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }

    private static string FormatPose(in Pose pose)
    {
        return $"{FormatVector3(pose.position)}|{FormatVector3(pose.rotation.eulerAngles)}";
    }

    private static string FormatPoseDelta(in Pose fromPose, in Pose toPose)
    {
        var positionDelta = toPose.position - fromPose.position;
        var rotationDelta = Quaternion.Inverse(fromPose.rotation) * toPose.rotation;
        return $"{FormatVector3(positionDelta)}|{FormatVector3(rotationDelta.eulerAngles)}";
    }

    private static string FormatIntrinsics(XrCameraIntrinsics intrinsics)
    {
        return
            $"fxfy=({intrinsics.focalLength.X:F2},{intrinsics.focalLength.Y:F2}) cxcy=({intrinsics.principalPoint.X:F2},{intrinsics.principalPoint.Y:F2}) fov=({intrinsics.fov.X:F2},{intrinsics.fov.Y:F2})";
    }

    private static string FormatProjectionModel(in CameraProjectionModel projectionModel)
    {
        return
            $"fxfy=({projectionModel.Fx:F2},{projectionModel.Fy:F2}) cxcy=({projectionModel.Cx:F2},{projectionModel.Cy:F2})";
    }

    private static string FormatMetricDefinition(QrMarkerDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        return
            $"size={definition.QrCodeSizeMeters:F3}m modules={Mathf.Max(21, definition.QrModuleCount)}";
    }

    private static string FormatNullableVector3(Vector3? value)
    {
        return value.HasValue ? FormatVector3(value.Value) : UnavailableValue;
    }

    private static string FormatNullableRayOrigin(Ray? ray)
    {
        return ray.HasValue ? FormatVector3(ray.Value.origin) : UnavailableValue;
    }

    private static string FormatNullableRayDirection(Ray? ray)
    {
        return ray.HasValue ? FormatVector3(ray.Value.direction.normalized) : UnavailableValue;
    }

    private static string FormatNullableHitPoint(RaycastHit? hit)
    {
        return hit.HasValue ? FormatVector3(hit.Value.point) : UnavailableValue;
    }

    private static string FormatNullableHitNormal(RaycastHit? hit)
    {
        return hit.HasValue ? FormatVector3(hit.Value.normal) : UnavailableValue;
    }

    private static string FormatFrameSize(int width, int height)
    {
        return $"{Mathf.Max(0, width)}x{Mathf.Max(0, height)}";
    }
}
