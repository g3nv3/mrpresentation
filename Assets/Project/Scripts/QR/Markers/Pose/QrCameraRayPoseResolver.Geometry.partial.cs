using Unity.XR.PXR;
using UnityEngine;
using ZXing;

public sealed partial class QrCameraRayPoseResolver
{
    private void PopulateDebugPointsFromRaycasts(CameraProjectionData context, in QrDetection detection)
    {
        debugResultPointPositions.Clear();

        var imageResultPoints = detection.ImageResultPoints;
        if (imageResultPoints != null)
        {
            for (var i = 0; i < imageResultPoints.Length; i++)
            {
                if (!TryBuildWorldRay(
                        context,
                        imageResultPoints[i],
                        detection.FrameWidth,
                        detection.FrameHeight,
                        GetResultPointsMirrorX(),
                        out var ray,
                        out _) ||
                    !TryRaycastWorldRay(ray, out var hit))
                {
                    continue;
                }

                debugResultPointPositions.Add(hit.point + GetSurfaceOffset(hit.normal));
            }
        }
    }

    private Pose ApplyPoseFilter(CameraProjectionData context, Pose resolvedPose)
    {
        var positionBlend = GetAdaptiveSmoothingBlend(
            positionSmoothing,
            context.CurrentDeviceLinearSpeedMetersPerSecond,
            context.CurrentDeviceAngularSpeedDegreesPerSecond);
        var rotationBlend = GetAdaptiveSmoothingBlend(
            rotationSmoothing,
            context.CurrentDeviceLinearSpeedMetersPerSecond,
            context.CurrentDeviceAngularSpeedDegreesPerSecond);
        lastPositionBlend = positionBlend;
        lastRotationBlend = rotationBlend;

        if (!hasFilteredPose ||
            (filteredPosition - resolvedPose.position).sqrMagnitude > snapDistance * snapDistance)
        {
            filteredPosition = resolvedPose.position;
            filteredRotation = resolvedPose.rotation;
            hasFilteredPose = true;
            return resolvedPose;
        }

        filteredPosition = Vector3.Lerp(filteredPosition, resolvedPose.position, positionBlend);
        filteredRotation = Quaternion.Slerp(filteredRotation, resolvedPose.rotation, rotationBlend);
        return new Pose(filteredPosition, filteredRotation);
    }

    private float GetAdaptiveSmoothingBlend(
        float baseBlend,
        float currentLinearSpeedMetersPerSecond,
        float currentAngularSpeedDegreesPerSecond)
    {
        if (!adaptSmoothingToDeviceMotion)
        {
            return baseBlend;
        }

        var linearFactor = currentLinearSpeedMetersPerSecond /
                           Mathf.Max(deviceMotionLinearThresholdMetersPerSecond, 0.001f);
        var angularFactor = currentAngularSpeedDegreesPerSecond /
                            Mathf.Max(deviceMotionAngularThresholdDegreesPerSecond, 0.1f);
        var motionFactor = Mathf.Clamp01(Mathf.Max(linearFactor, angularFactor));
        return Mathf.Lerp(baseBlend, 1f, motionFactor);
    }

    private bool TryGetOrderedFinderSurfaceHits(
        CameraProjectionData context,
        in QrDetection detection,
        out Vector3 bottomLeftWorld,
        out Vector3 topLeftWorld,
        out Vector3 topRightWorld)
    {
        bottomLeftWorld = default;
        topLeftWorld = default;
        topRightWorld = default;

        if (!TryGetOrderedFinderPoints(
                detection.ImageResultPoints,
                out var bottomLeftImage,
                out var topLeftImage,
                out var topRightImage))
        {
            return false;
        }

        return TryGetSurfaceHitPoint(context, bottomLeftImage, detection.FrameWidth, detection.FrameHeight, out bottomLeftWorld) &&
               TryGetSurfaceHitPoint(context, topLeftImage, detection.FrameWidth, detection.FrameHeight, out topLeftWorld) &&
               TryGetSurfaceHitPoint(context, topRightImage, detection.FrameWidth, detection.FrameHeight, out topRightWorld);
    }

    private bool TryGetSurfaceHitPoint(
        CameraProjectionData context,
        Vector2 imagePoint,
        int frameWidth,
        int frameHeight,
        out Vector3 worldPoint)
    {
        worldPoint = default;
        if (!TryBuildWorldRay(
                context,
                imagePoint,
                frameWidth,
                frameHeight,
                GetResultPointsMirrorX(),
                out var ray,
                out _) ||
            !TryRaycastWorldRay(ray, out var hit))
        {
            return false;
        }

        worldPoint = hit.point + GetSurfaceOffset(hit.normal);
        return true;
    }

    private static bool TryGetOrderedFinderPoints(
        Vector2[] imageResultPoints,
        out Vector2 bottomLeft,
        out Vector2 topLeft,
        out Vector2 topRight)
    {
        bottomLeft = default;
        topLeft = default;
        topRight = default;

        if (imageResultPoints == null)
        {
            return false;
        }

        var validPoints = new ResultPoint[3];
        var validCount = 0;
        for (var i = 0; i < imageResultPoints.Length && validCount < validPoints.Length; i++)
        {
            if (!IsFinite(imageResultPoints[i]))
            {
                continue;
            }

            validPoints[validCount++] = new ResultPoint(imageResultPoints[i].x, imageResultPoints[i].y);
        }

        if (validCount < 3)
        {
            return false;
        }

        ResultPoint.orderBestPatterns(validPoints);
        bottomLeft = new Vector2(validPoints[0].X, validPoints[0].Y);
        topLeft = new Vector2(validPoints[1].X, validPoints[1].Y);
        topRight = new Vector2(validPoints[2].X, validPoints[2].Y);
        return true;
    }

    private static int GetPointCount(Vector2[] points)
    {
        return points?.Length ?? 0;
    }

    private static float Magnitude(PxrVector3f vector)
    {
        return Mathf.Sqrt(
            (vector.x * vector.x) +
            (vector.y * vector.y) +
            (vector.z * vector.z));
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

    private Vector3 GetSurfaceOffset(Vector3 normal)
    {
        var hitNormal = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
        return hitNormal * surfaceOffset;
    }
}
