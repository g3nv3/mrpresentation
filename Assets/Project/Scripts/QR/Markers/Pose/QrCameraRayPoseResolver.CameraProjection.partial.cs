using Unity.XR.PXR;
using UnityEngine;

public sealed partial class QrCameraRayPoseResolver
{
    private bool TryCreateRayContext(Camera activeCamera, in QrDetection detection, out CameraProjectionData context)
    {
        var trackingOrigin = activeCamera.transform.parent;
        var trackingOriginWorldPose = trackingOrigin != null
            ? new Pose(trackingOrigin.position, trackingOrigin.rotation)
            : Pose.identity;

        if (!TryGetCurrentDeviceWorldPose(
                trackingOriginWorldPose,
                out var currentHeadWorldPose,
                out var currentDeviceLinearSpeedMetersPerSecond,
                out var currentDeviceAngularSpeedDegreesPerSecond,
                out var currentDeviceNote))
        {
            currentHeadWorldPose = new Pose(activeCamera.transform.position, activeCamera.transform.rotation);
            currentDeviceLinearSpeedMetersPerSecond = 0f;
            currentDeviceAngularSpeedDegreesPerSecond = 0f;
            currentDeviceNote = "current-device(fallback-camera)";
        }
        var headWorldPose = currentHeadWorldPose;
        var note = currentDeviceNote;
        var useCaptureHeadPose = false;

        if (useCaptureTimeHeadPose &&
            TryGetHeadWorldPoseAtCaptureTime(trackingOriginWorldPose, detection.CaptureTime, out var captureHeadPose, out var captureNote))
        {
            headWorldPose = captureHeadPose;
            note = captureNote;
            useCaptureHeadPose = true;
        }

        if (!TryGetProjectionData(detection.CameraId, out var projectionData))
        {
            if (string.Equals(lastDebugStatus, "ray: started"))
            {
                lastDebugStatus = "ray: context failed";
            }

            context = default;
            return false;
        }

        var adjustedCameraLocalPose = AdjustReportedCameraLocalPose(projectionData.CameraLocalPose);
        var cameraWorldPose = ResolveCameraWorldPose(
            headWorldPose,
            trackingOriginWorldPose,
            adjustedCameraLocalPose);
        context = new CameraProjectionData(
            projectionData.Intrinsics,
            projectionData.CameraLocalPose,
            adjustedCameraLocalPose,
            headWorldPose,
            currentHeadWorldPose,
            currentDeviceLinearSpeedMetersPerSecond,
            currentDeviceAngularSpeedDegreesPerSecond,
            trackingOriginWorldPose,
            cameraWorldPose,
            useCaptureHeadPose ? "intrinsics-capture-device" : "intrinsics-current-device",
            note);
        return true;
    }

    private bool TryBuildWorldRay(
        CameraProjectionData context,
        Vector2 imagePoint,
        int frameWidth,
        int frameHeight,
        bool mirrorX,
        out Ray ray,
        out Vector2 adjustedImagePoint)
    {
        ray = default;
        adjustedImagePoint = default;
        if (!TryAdjustImagePoint(imagePoint, frameWidth, frameHeight, mirrorX, out adjustedImagePoint))
        {
            return false;
        }

        if (!TryBuildProjectionModel(context.Intrinsics, frameWidth, frameHeight, out var projectionModel))
        {
            return false;
        }

        var localDirection = BuildCameraSpaceDirection(
            projectionModel,
            adjustedImagePoint,
            invertCameraSpaceForward);
        if (localDirection.sqrMagnitude < MinAxisMagnitude)
        {
            return false;
        }

        ray = new Ray(
            context.CameraWorldPose.position,
            context.CameraWorldPose.rotation * localDirection.normalized);
        return true;
    }

    private bool TryRaycastWorldRay(in Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, maxDistance, raycastMask);
    }

    private bool TryAdjustImagePoint(Vector2 imagePoint, int frameWidth, int frameHeight, bool mirrorX, out Vector2 adjustedImagePoint)
    {
        adjustedImagePoint = default;
        if (frameWidth <= 0 || frameHeight <= 0 || !IsFinite(imagePoint))
        {
            return false;
        }

        adjustedImagePoint = new Vector2(
            Mathf.Clamp(imagePoint.x, 0f, frameWidth),
            Mathf.Clamp(imagePoint.y, 0f, frameHeight));
        adjustedImagePoint.x = Mathf.Clamp(adjustedImagePoint.x + viewportOffset.x * frameWidth, 0f, frameWidth);
        adjustedImagePoint.y = Mathf.Clamp(adjustedImagePoint.y + viewportOffset.y * frameHeight, 0f, frameHeight);

        if (mirrorX)
        {
            adjustedImagePoint.x = Mathf.Clamp(frameWidth - adjustedImagePoint.x, 0f, frameWidth);
        }

        return true;
    }

    private static Vector3 BuildCameraSpaceDirection(
        in CameraProjectionModel projectionModel,
        Vector2 imagePoint,
        bool invertForwardZ)
    {
        var fx = Mathf.Max(projectionModel.Fx, MinAxisMagnitude);
        var fy = Mathf.Max(projectionModel.Fy, MinAxisMagnitude);
        var x = (imagePoint.x - projectionModel.Cx) / fx;
        var y = (projectionModel.Cy - imagePoint.y) / fy;
        return new Vector3(x, y, invertForwardZ ? -1f : 1f);
    }

    private bool TryBuildProjectionModel(
        XrCameraIntrinsics intrinsics,
        int frameWidth,
        int frameHeight,
        out CameraProjectionModel projectionModel)
    {
        projectionModel = default;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return false;
        }

        if (projectionModelMode == ProjectionModelMode.DeriveFromFrameFov)
        {
            var horizontalFovRadians = intrinsics.fov.X * Mathf.Deg2Rad;
            var verticalFovRadians = intrinsics.fov.Y * Mathf.Deg2Rad;
            var halfHorizontalTangent = Mathf.Tan(horizontalFovRadians * 0.5f);
            var halfVerticalTangent = Mathf.Tan(verticalFovRadians * 0.5f);
            if (horizontalFovRadians > MinAxisMagnitude &&
                verticalFovRadians > MinAxisMagnitude &&
                !float.IsNaN(halfHorizontalTangent) &&
                !float.IsInfinity(halfHorizontalTangent) &&
                !float.IsNaN(halfVerticalTangent) &&
                !float.IsInfinity(halfVerticalTangent) &&
                Mathf.Abs(halfHorizontalTangent) > MinAxisMagnitude &&
                Mathf.Abs(halfVerticalTangent) > MinAxisMagnitude)
            {
                projectionModel = new CameraProjectionModel(
                    frameWidth * 0.5f / halfHorizontalTangent,
                    frameHeight * 0.5f / halfVerticalTangent,
                    frameWidth * 0.5f,
                    frameHeight * 0.5f);
                return true;
            }
        }

        projectionModel = new CameraProjectionModel(
            intrinsics.focalLength.X,
            intrinsics.focalLength.Y,
            intrinsics.principalPoint.X,
            intrinsics.principalPoint.Y);
        return projectionModel.IsValid;
    }

    private bool TryGetProjectionData(XrCameraIdPICO cameraId, out CameraProjectionData projectionData)
    {
        if (hasCachedProjectionData && cachedCameraId == cameraId)
        {
            projectionData = cachedProjectionData;
            return true;
        }

        projectionData = default;
        if (PXR_CameraImage.GetCameraIntrinsics(cameraId, out var intrinsics) != PxrResult.SUCCESS)
        {
            lastDebugStatus = $"ray: intrinsics unavailable, camera={cameraId}";
            return false;
        }

        if (PXR_CameraImage.GetCameraExtrinsics(cameraId, out var extrinsics) != PxrResult.SUCCESS)
        {
            lastDebugStatus = $"ray: extrinsics unavailable, camera={cameraId}";
            return false;
        }

        projectionData = new CameraProjectionData(
            intrinsics,
            new Pose(
                new Vector3(extrinsics.pose.Position.X, extrinsics.pose.Position.Y, extrinsics.pose.Position.Z),
                NormalizeQuaternion(new Quaternion(
                    extrinsics.pose.Orientation.X,
                    extrinsics.pose.Orientation.Y,
                    extrinsics.pose.Orientation.Z,
                    extrinsics.pose.Orientation.W))));

        cachedCameraId = cameraId;
        cachedProjectionData = projectionData;
        hasCachedProjectionData = true;
        return true;
    }

    private Pose AdjustReportedCameraLocalPose(in Pose reportedCameraPose)
    {
        var position = ignoreReportedCameraTranslation ? Vector3.zero : reportedCameraPose.position;
        if (!ignoreReportedCameraTranslation && flipReportedCameraTranslationZ)
        {
            position.z = -position.z;
        }

        var rotation = ignoreReportedCameraRotation ? Quaternion.identity : reportedCameraPose.rotation;
        return new Pose(position, rotation);
    }

    private bool TryGetCurrentDeviceWorldPose(
        in Pose trackingOriginWorldPose,
        out Pose deviceWorldPose,
        out float linearSpeedMetersPerSecond,
        out float angularSpeedDegreesPerSecond,
        out string note)
    {
        deviceWorldPose = default;
        linearSpeedMetersPerSecond = 0f;
        angularSpeedDegreesPerSecond = 0f;
        note = null;

        var sensorFrameIndex = 0;
        var sensorState = default(PxrSensorState2);
        if (PXR_System.GetPredictedMainSensorStateNew(ref sensorState, ref sensorFrameIndex) != 0)
        {
            return false;
        }

        deviceWorldPose = ConvertPluginPose(sensorState.pose).GetTransformedBy(trackingOriginWorldPose);
        linearSpeedMetersPerSecond = Magnitude(sensorState.linearVelocity);
        angularSpeedDegreesPerSecond = Magnitude(sensorState.angularVelocity) * Mathf.Rad2Deg;
        note = $"current-device frame={sensorFrameIndex}";
        return true;
    }

    private bool TryGetHeadWorldPoseAtCaptureTime(
        in Pose trackingOriginWorldPose,
        long captureTime,
        out Pose headWorldPose,
        out string note)
    {
        headWorldPose = default;
        note = null;

        var predictTimeMs = ConvertCaptureTimeToPredictTimeMs(captureTime);
        if (predictTimeMs <= 0d)
        {
            return false;
        }

        var sensorFrameIndex = 0;
        var sensorState = default(PxrSensorState2);
        if (PXR_Plugin.Pxr_GetPredictedMainSensorState2(predictTimeMs, ref sensorState, ref sensorFrameIndex) != 0)
        {
            return false;
        }

        headWorldPose = ConvertPluginPose(sensorState.pose).GetTransformedBy(trackingOriginWorldPose);
        note = $"capture-device={predictTimeMs:F1}ms frame={sensorFrameIndex}";
        return true;
    }

    private static double ConvertCaptureTimeToPredictTimeMs(long captureTime)
    {
        if (captureTime <= 0)
        {
            return 0d;
        }

        if (captureTime >= 1_000_000_000_000L)
        {
            return captureTime / 1_000_000d;
        }

        if (captureTime >= 1_000_000_000L)
        {
            return captureTime / 1_000d;
        }

        return captureTime;
    }

    private static Pose ConvertPluginPose(PxrPosef pose)
    {
        return new Pose(
            new Vector3(pose.position.x, pose.position.y, -pose.position.z),
            NormalizeQuaternion(new Quaternion(
                pose.orientation.x,
                pose.orientation.y,
                -pose.orientation.z,
                -pose.orientation.w)));
    }

    private static Quaternion NormalizeQuaternion(Quaternion rotation)
    {
        var magnitude = Mathf.Sqrt(
            rotation.x * rotation.x +
            rotation.y * rotation.y +
            rotation.z * rotation.z +
            rotation.w * rotation.w);

        if (magnitude < MinAxisMagnitude)
        {
            return Quaternion.identity;
        }

        return new Quaternion(
            rotation.x / magnitude,
            rotation.y / magnitude,
            rotation.z / magnitude,
            rotation.w / magnitude);
    }

    private static Pose TransformPose(in Pose parentPose, in Pose localPose)
    {
        return new Pose(
            parentPose.position + parentPose.rotation * localPose.position,
            parentPose.rotation * localPose.rotation);
    }

    private Pose ResolveCameraWorldPose(
        in Pose headWorldPose,
        in Pose trackingOriginWorldPose,
        in Pose reportedCameraPose)
    {
        return cameraExtrinsicsInterpretation switch
        {
            CameraExtrinsicsInterpretation.InverseOfReportedPose =>
                TransformPose(headWorldPose, InvertPose(reportedCameraPose)),
            CameraExtrinsicsInterpretation.TrackingSpacePose =>
                TransformPose(trackingOriginWorldPose, reportedCameraPose),
            _ => TransformPose(headWorldPose, reportedCameraPose)
        };
    }

    private bool GetResultPointsMirrorX()
    {
        return lockResultPointMirrorToCenter ? mirrorImageX : mirrorResultPointsX;
    }

    private static Pose InvertPose(in Pose pose)
    {
        var inverseRotation = Quaternion.Inverse(pose.rotation);
        return new Pose(-(inverseRotation * pose.position), inverseRotation);
    }

    private readonly struct CameraProjectionData
    {
        public readonly XrCameraIntrinsics Intrinsics;
        public readonly Pose RawCameraLocalPose;
        public readonly Pose CameraLocalPose;
        public readonly Pose HeadWorldPose;
        public readonly Pose CurrentHeadWorldPose;
        public readonly float CurrentDeviceLinearSpeedMetersPerSecond;
        public readonly float CurrentDeviceAngularSpeedDegreesPerSecond;
        public readonly Pose TrackingOriginWorldPose;
        public readonly Pose CameraWorldPose;
        public readonly string Mode;
        public readonly string Note;

        public CameraProjectionData(XrCameraIntrinsics intrinsics, in Pose cameraLocalPose)
            : this(intrinsics, cameraLocalPose, cameraLocalPose, default, default, 0f, 0f, default, default, null, null)
        {
        }

        public CameraProjectionData(
            XrCameraIntrinsics intrinsics,
            in Pose rawCameraLocalPose,
            in Pose cameraLocalPose,
            in Pose headWorldPose,
            in Pose currentHeadWorldPose,
            float currentDeviceLinearSpeedMetersPerSecond,
            float currentDeviceAngularSpeedDegreesPerSecond,
            in Pose trackingOriginWorldPose,
            in Pose cameraWorldPose,
            string mode,
            string note)
        {
            Intrinsics = intrinsics;
            RawCameraLocalPose = rawCameraLocalPose;
            CameraLocalPose = cameraLocalPose;
            HeadWorldPose = headWorldPose;
            CurrentHeadWorldPose = currentHeadWorldPose;
            CurrentDeviceLinearSpeedMetersPerSecond = currentDeviceLinearSpeedMetersPerSecond;
            CurrentDeviceAngularSpeedDegreesPerSecond = currentDeviceAngularSpeedDegreesPerSecond;
            TrackingOriginWorldPose = trackingOriginWorldPose;
            CameraWorldPose = cameraWorldPose;
            Mode = mode;
            Note = note;
        }
    }

    private readonly struct CameraProjectionModel
    {
        public readonly float Fx;
        public readonly float Fy;
        public readonly float Cx;
        public readonly float Cy;

        public bool IsValid =>
            Fx > MinAxisMagnitude &&
            Fy > MinAxisMagnitude &&
            !float.IsNaN(Cx) &&
            !float.IsInfinity(Cx) &&
            !float.IsNaN(Cy) &&
            !float.IsInfinity(Cy);

        public CameraProjectionModel(float fx, float fy, float cx, float cy)
        {
            Fx = fx;
            Fy = fy;
            Cx = cx;
            Cy = cy;
        }
    }
}
