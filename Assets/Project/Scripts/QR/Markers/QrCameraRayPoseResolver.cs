using System;
using System.Collections.Generic;

using UnityEngine;
using Unity.XR.PXR;

public sealed class QrCameraRayPoseResolver : MonoBehaviour, IQrPoseResolver
{
    private enum CameraExtrinsicsInterpretation
    {
        CameraPoseRelativeToDevice = 0,
        InverseOfReportedPose = 1
    }

    private const float MinAxisMagnitude = 0.0001f;
    private const string UnavailableValue = "-";

    [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private bool useCaptureTimeHeadPose;
    [SerializeField] private bool mirrorImageX;
    [SerializeField] private bool mirrorResultPointsX;
    [SerializeField] private bool lockResultPointMirrorToCenter = true;
    [SerializeField] private bool invertCameraSpaceForward;
    [SerializeField] private CameraExtrinsicsInterpretation cameraExtrinsicsInterpretation =
        CameraExtrinsicsInterpretation.CameraPoseRelativeToDevice;
    [SerializeField] private Vector2 viewportOffset;
    [SerializeField] private bool orientUsingQrResultPoints = true;
    [SerializeField, Range(0f, 1f)] private float positionSmoothing = 0.25f;
    [SerializeField, Range(0f, 1f)] private float rotationSmoothing = 0.25f;
    [SerializeField] private float snapDistance = 0.2f;
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private bool enableMetricPoseRefinement = true;
    [SerializeField, Range(1, 12)] private int metricPoseRefinementIterations = 6;
    [SerializeField, Min(0.0001f)] private float metricPoseRotationStepRadians = 0.01f;
    [SerializeField, Min(0.0001f)] private float metricPoseTranslationStepMeters = 0.005f;
    [SerializeField, Min(0.000001f)] private float metricPoseInitialDamping = 0.001f;
    [SerializeField, Min(0f)] private float metricPoseMaxTranslationDelta = 0.25f;
    [SerializeField, Range(0f, 180f)] private float metricPoseMaxRotationDeltaDegrees = 35f;
    [SerializeField] private bool verbosePoseDiagnostics;
    [SerializeField, Min(0.1f)] private float poseDiagnosticsInterval = 0.5f;
    [SerializeField] private QrPoseResolverDebugView debugView = new QrPoseResolverDebugView();

    private readonly List<Vector3> debugResultPointPositions = new List<Vector3>(4);
    private bool hasCachedProjectionData;
    private XrCameraIdPICO cachedCameraId;
    private CameraProjectionData cachedProjectionData;
    private string lastDebugStatus = "ray: init";
    private float nextPoseDiagnosticsTime;
    private Ray lastRay;
    private bool hasLastRay;
    private Vector3 filteredPosition;
    private Quaternion filteredRotation = Quaternion.identity;
    private bool hasFilteredPose;

    public string LastDebugStatus => lastDebugStatus;
    public bool HasLastRay => hasLastRay;
    public Ray LastRay => lastRay;

    private void Awake()
    {
        GetDebugView().Initialize(transform);
        GetDebugView().Hide();
    }

    public bool TryResolvePose(in QrDetection detection, QrMarkerDefinition definition, out Pose pose)
    {
        pose = default;
        lastDebugStatus = "ray: started";

        var currentDebugView = GetDebugView();
        if (!detection.HasImageCenter || detection.FrameWidth <= 0 || detection.FrameHeight <= 0)
        {
            hasLastRay = false;
            lastRay = default;
            lastDebugStatus =
                "ray: no image center\n" +
                $"frame={FormatFrameSize(detection.FrameWidth, detection.FrameHeight)}\n" +
                $"points={GetPointCount(detection.ImageResultPoints)}";
            currentDebugView.Hide();
            return false;
        }

        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            hasLastRay = false;
            lastRay = default;
            lastDebugStatus =
                "ray: no target camera\n" +
                $"frame={FormatFrameSize(detection.FrameWidth, detection.FrameHeight)}\n" +
                $"raw={FormatVector2(detection.ImageCenter)}";
            currentDebugView.Hide();
            return false;
        }

        if (!TryCreateRayContext(activeCamera, detection, out var context))
        {
            hasLastRay = false;
            lastRay = default;
            lastDebugStatus = BuildDebugStatus(
                "ray: context failed",
                detection,
                null,
                null,
                null,
                null,
                null);
            currentDebugView.Hide();
            return false;
        }

        if (!TryBuildWorldRay(
                context,
                detection.ImageCenter,
                detection.FrameWidth,
                detection.FrameHeight,
                mirrorImageX,
                out var ray,
                out var adjustedImagePoint))
        {
            hasLastRay = false;
            lastRay = default;
            lastDebugStatus = BuildDebugStatus(
                "ray: build failed",
                detection,
                context,
                null,
                null,
                null,
                null);
            currentDebugView.Hide();
            return false;
        }

        lastRay = ray;
        hasLastRay = true;

        if (!TryRaycastWorldRay(ray, out var centerHit))
        {
            debugResultPointPositions.Clear();
            PopulateDebugPointsFromRaycasts(context, detection);
            UpdateDebugView(null);
            lastDebugStatus = BuildDebugStatus(
                "ray: no surface hit",
                detection,
                context,
                adjustedImagePoint,
                ray,
                null,
                null);
            MaybeLog("miss", detection, context, ray);
            return false;
        }

        var hitNormal = centerHit.normal.sqrMagnitude > 0f ? centerHit.normal.normalized : Vector3.up;
        var direction = ray.direction.sqrMagnitude >= MinAxisMagnitude
            ? ray.direction.normalized
            : context.CameraWorldPose.forward;

        debugResultPointPositions.Clear();

        var forward = Vector3.zero;
        if (orientUsingQrResultPoints &&
            TryGetOrderedFinderSurfaceHits(
                context,
                detection,
                out var bottomLeftWorld,
                out var topLeftWorld,
                out var topRightWorld))
        {
            debugResultPointPositions.Add(bottomLeftWorld);
            debugResultPointPositions.Add(topLeftWorld);
            debugResultPointPositions.Add(topRightWorld);

            var qrUpOnSurface = Vector3.ProjectOnPlane(topLeftWorld - bottomLeftWorld, hitNormal);
            if (qrUpOnSurface.sqrMagnitude >= MinAxisMagnitude)
            {
                forward = qrUpOnSurface.normalized;
            }
        }

        if (forward.sqrMagnitude < MinAxisMagnitude)
        {
            PopulateDebugPointsFromRaycasts(context, detection);
            forward = Vector3.ProjectOnPlane(context.CameraWorldPose.forward, hitNormal);
        }

        if (forward.sqrMagnitude < MinAxisMagnitude)
        {
            forward = Vector3.ProjectOnPlane(direction, hitNormal);
        }

        var up = hitNormal;
        if (up.sqrMagnitude < MinAxisMagnitude)
        {
            up = Vector3.up;
        }

        if (forward.sqrMagnitude < MinAxisMagnitude)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude < MinAxisMagnitude)
        {
            forward = Vector3.Cross(up, Vector3.forward);
        }

        var resolvedPosition = centerHit.point + GetSurfaceOffset(hitNormal);
        var resolvedPose = new Pose(
            resolvedPosition,
            Quaternion.LookRotation(forward.normalized, up));
        var metricPoseApplied = TryRefineMetricPose(
            context,
            detection,
            definition,
            resolvedPose,
            out var metricPose,
            out var metricPoseStatus);
        if (metricPoseApplied)
        {
            resolvedPose = metricPose;
            PopulateDebugPointsFromPose(definition, resolvedPose);
        }

        resolvedPose = ApplyPoseFilter(resolvedPose);

        pose = resolvedPose;

        UpdateDebugView(resolvedPose.position);
        lastDebugStatus = BuildDebugStatus(
            "ray ok: surface hit",
            detection,
            context,
            adjustedImagePoint,
            ray,
            centerHit,
            resolvedPosition);
        if (!string.IsNullOrWhiteSpace(metricPoseStatus))
        {
            lastDebugStatus += "\n" + metricPoseStatus;
        }

        MaybeLog(metricPoseApplied ? "metric-refined" : "surface-hit", detection, context, ray);
        return true;
    }

    public void ClearResolvedPose()
    {
        hasLastRay = false;
        hasFilteredPose = false;
        filteredPosition = default;
        filteredRotation = Quaternion.identity;
        lastRay = default;
        lastDebugStatus = "ray: cleared";
        nextPoseDiagnosticsTime = 0f;
        debugResultPointPositions.Clear();
        GetDebugView().Hide();
    }

    public bool TryGetLastRay(out Ray ray)
    {
        ray = lastRay;
        return hasLastRay;
    }

    private void UpdateDebugView(Vector3? centerPosition)
    {
        GetDebugView().Show(centerPosition, debugResultPointPositions);
    }

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

    private bool TryCreateRayContext(Camera activeCamera, in QrDetection detection, out CameraProjectionData context)
    {
        var headWorldPose = new Pose(activeCamera.transform.position, activeCamera.transform.rotation);
        var note = "current head";
        var useCaptureHeadPose = false;

        if (useCaptureTimeHeadPose &&
            TryGetHeadWorldPoseAtCaptureTime(activeCamera, detection.CaptureTime, out var captureHeadPose, out var captureNote))
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

        var cameraWorldPose = TransformPose(
            headWorldPose,
            ResolveCameraLocalPose(projectionData.CameraLocalPose));
        context = new CameraProjectionData(
            projectionData.Intrinsics,
            projectionData.CameraLocalPose,
            cameraWorldPose,
            useCaptureHeadPose ? "intrinsics-capture-head" : "intrinsics-current-head",
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

        var localDirection = BuildCameraSpaceDirection(
            context.Intrinsics,
            adjustedImagePoint,
            frameWidth,
            frameHeight,
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

        return outcome + "\n" +
               $"mode={mode}\n" +
               $"frame={FormatFrameSize(detection.FrameWidth, detection.FrameHeight)}\n" +
               $"centerMirrorX={(mirrorImageX ? "1" : "0")} pointsMirrorX={(GetResultPointsMirrorX() ? "1" : "0")}\n" +
               $"forwardZ={(invertCameraSpaceForward ? "-1" : "+1")}\n" +
               $"extrinsics={cameraExtrinsicsInterpretation}\n" +
               $"qrBasis={(orientUsingQrResultPoints ? "finder" : "head")}\n" +
               $"raw={FormatVector2(detection.ImageCenter)} adjusted={FormatNullableVector2(adjustedImagePoint)}\n" +
               $"origin={FormatNullableRayOrigin(ray)}\n" +
               $"dir={FormatNullableRayDirection(ray)}\n" +
               $"hit={FormatNullableHitPoint(hit)} normal={FormatNullableHitNormal(hit)}\n" +
               $"resolved={FormatNullableVector3(resolvedPosition)}\n" +
               $"note={note}";
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
        XrCameraIntrinsics intrinsics,
        Vector2 imagePoint,
        int frameWidth,
        int frameHeight,
        bool invertForwardZ)
    {
        imagePoint = ScaleImagePointToIntrinsics(intrinsics, imagePoint, frameWidth, frameHeight);
        var fx = Mathf.Max(intrinsics.focalLength.X, MinAxisMagnitude);
        var fy = Mathf.Max(intrinsics.focalLength.Y, MinAxisMagnitude);
        var x = (imagePoint.x - intrinsics.principalPoint.X) / fx;
        var y = (intrinsics.principalPoint.Y - imagePoint.y) / fy;
        return new Vector3(x, y, invertForwardZ ? -1f : 1f);
    }

    private static Vector2 ScaleImagePointToIntrinsics(
        XrCameraIntrinsics intrinsics,
        Vector2 imagePoint,
        int frameWidth,
        int frameHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return imagePoint;
        }

        var intrinsicsWidth = Mathf.Max(intrinsics.principalPoint.X * 2f, MinAxisMagnitude);
        var intrinsicsHeight = Mathf.Max(intrinsics.principalPoint.Y * 2f, MinAxisMagnitude);
        return new Vector2(
            imagePoint.x * intrinsicsWidth / frameWidth,
            imagePoint.y * intrinsicsHeight / frameHeight);
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

    private bool TryGetHeadWorldPoseAtCaptureTime(
        Camera activeCamera,
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

        var trackingOrigin = activeCamera.transform.parent;
        var trackingOriginWorldPose = trackingOrigin != null
            ? new Pose(trackingOrigin.position, trackingOrigin.rotation)
            : Pose.identity;

        headWorldPose = ConvertPluginPose(sensorState.pose).GetTransformedBy(trackingOriginWorldPose);
        note = $"capture={predictTimeMs:F1}ms frame={sensorFrameIndex}";
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

    private bool TryRefineMetricPose(
        CameraProjectionData context,
        in QrDetection detection,
        QrMarkerDefinition definition,
        in Pose basePose,
        out Pose refinedPose,
        out string metricPoseStatus)
    {
        refinedPose = default;

        if (!enableMetricPoseRefinement)
        {
            metricPoseStatus = "metric=disabled";
            return false;
        }

        if (definition == null || !definition.HasMetricPoseConfiguration)
        {
            metricPoseStatus = "metric=skipped(no-config)";
            return false;
        }

        if (!TryGetObservedFinderPoints(context.Intrinsics, detection, out var observedFinderPoints))
        {
            metricPoseStatus = "metric=skipped(no-finders)";
            return false;
        }

        var localFinderPoints = BuildFinderLocalPoints(definition);
        if (!TryOptimizeMetricPose(
                context,
                basePose,
                localFinderPoints,
                observedFinderPoints,
                out refinedPose,
                out var reprojectionError))
        {
            metricPoseStatus = "metric=skipped(opt-failed)";
            return false;
        }

        var translationDelta = Vector3.Distance(basePose.position, refinedPose.position);
        var rotationDelta = Quaternion.Angle(basePose.rotation, refinedPose.rotation);
        if (translationDelta > metricPoseMaxTranslationDelta ||
            rotationDelta > metricPoseMaxRotationDeltaDegrees)
        {
            metricPoseStatus =
                $"metric=skipped(outlier dt={translationDelta:F3} dr={rotationDelta:F1})";
            return false;
        }

        metricPoseStatus =
            $"metric=refined err={reprojectionError:F2}px dt={translationDelta:F3} dr={rotationDelta:F1}";
        return true;
    }

    private bool TryGetObservedFinderPoints(
        XrCameraIntrinsics intrinsics,
        in QrDetection detection,
        out Vector2[] observedFinderPoints)
    {
        observedFinderPoints = null;
        if (!TryGetOrderedFinderPoints(
                detection.ImageResultPoints,
                out var bottomLeftImage,
                out var topLeftImage,
                out var topRightImage))
        {
            return false;
        }

        observedFinderPoints = new Vector2[3];
        return TryAdjustAndScaleFinderPoint(
                   intrinsics,
                   bottomLeftImage,
                   detection.FrameWidth,
                   detection.FrameHeight,
                   out observedFinderPoints[0]) &&
               TryAdjustAndScaleFinderPoint(
                   intrinsics,
                   topLeftImage,
                   detection.FrameWidth,
                   detection.FrameHeight,
                   out observedFinderPoints[1]) &&
               TryAdjustAndScaleFinderPoint(
                   intrinsics,
                   topRightImage,
                   detection.FrameWidth,
                   detection.FrameHeight,
                   out observedFinderPoints[2]);
    }

    private bool TryAdjustAndScaleFinderPoint(
        XrCameraIntrinsics intrinsics,
        Vector2 imagePoint,
        int frameWidth,
        int frameHeight,
        out Vector2 adjustedFinderPoint)
    {
        adjustedFinderPoint = default;
        if (!TryAdjustImagePoint(imagePoint, frameWidth, frameHeight, GetResultPointsMirrorX(), out var adjustedImagePoint))
        {
            return false;
        }

        adjustedFinderPoint = ScaleImagePointToIntrinsics(
            intrinsics,
            adjustedImagePoint,
            frameWidth,
            frameHeight);
        return IsFinite(adjustedFinderPoint);
    }

    private static Vector3[] BuildFinderLocalPoints(QrMarkerDefinition definition)
    {
        var moduleCount = Mathf.Max(21, definition.QrModuleCount);
        var finderCenterSpacing = definition.QrCodeSizeMeters * (moduleCount - 7f) / moduleCount;
        var halfSpacing = finderCenterSpacing * 0.5f;
        return new[]
        {
            new Vector3(-halfSpacing, 0f, -halfSpacing),
            new Vector3(-halfSpacing, 0f, halfSpacing),
            new Vector3(halfSpacing, 0f, halfSpacing)
        };
    }

    private bool TryOptimizeMetricPose(
        CameraProjectionData context,
        in Pose basePose,
        Vector3[] localFinderPoints,
        Vector2[] observedFinderPoints,
        out Pose refinedPose,
        out float reprojectionErrorPixels)
    {
        refinedPose = default;
        reprojectionErrorPixels = 0f;

        var cameraFromWorld = InvertPose(context.CameraWorldPose);
        var markerInCameraPose = TransformPose(cameraFromWorld, basePose);
        if (!TryComputeReprojectionError(
                context.Intrinsics,
                markerInCameraPose,
                localFinderPoints,
                observedFinderPoints,
                out var residuals,
                out var currentCost))
        {
            return false;
        }

        var damping = Mathf.Max(metricPoseInitialDamping, 0.000001f);
        for (var iteration = 0; iteration < Mathf.Max(1, metricPoseRefinementIterations); iteration++)
        {
            if (!TryBuildNormalEquations(
                    context.Intrinsics,
                    markerInCameraPose,
                    localFinderPoints,
                    observedFinderPoints,
                    residuals,
                    damping,
                    out var normalMatrix,
                    out var gradient))
            {
                break;
            }

            if (!TrySolveLinearSystem6x6(normalMatrix, gradient, out var delta))
            {
                break;
            }

            var candidatePose = ApplyPoseDelta(markerInCameraPose, delta);
            if (!TryComputeReprojectionError(
                    context.Intrinsics,
                    candidatePose,
                    localFinderPoints,
                    observedFinderPoints,
                    out var candidateResiduals,
                    out var candidateCost))
            {
                damping *= 4f;
                continue;
            }

            if (candidateCost + 0.0001f < currentCost)
            {
                markerInCameraPose = candidatePose;
                residuals = candidateResiduals;
                currentCost = candidateCost;
                damping = Mathf.Max(damping * 0.5f, 0.000001f);

                if (GetDeltaMagnitude(delta) <= 0.0001f)
                {
                    break;
                }
            }
            else
            {
                damping *= 4f;
            }
        }

        refinedPose = TransformPose(context.CameraWorldPose, markerInCameraPose);
        reprojectionErrorPixels = Mathf.Sqrt(currentCost / Mathf.Max(1, observedFinderPoints.Length * 2));
        return true;
    }

    private bool TryBuildNormalEquations(
        XrCameraIntrinsics intrinsics,
        in Pose pose,
        Vector3[] localFinderPoints,
        Vector2[] observedFinderPoints,
        float[] residuals,
        float damping,
        out float[,] normalMatrix,
        out float[] gradient)
    {
        const int parameterCount = 6;
        normalMatrix = new float[parameterCount, parameterCount];
        gradient = new float[parameterCount];
        var jacobianColumns = new float[parameterCount][];

        for (var parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
        {
            var step = parameterIndex < 3
                ? metricPoseRotationStepRadians
                : metricPoseTranslationStepMeters;
            var perturbedPose = ApplyPoseDelta(pose, CreateUnitDelta(parameterIndex, step));

            if (!TryComputeReprojectionError(
                    intrinsics,
                    perturbedPose,
                    localFinderPoints,
                    observedFinderPoints,
                    out var perturbedResiduals,
                    out _))
            {
                return false;
            }

            var jacobianColumn = new float[residuals.Length];
            jacobianColumns[parameterIndex] = jacobianColumn;
            for (var residualIndex = 0; residualIndex < residuals.Length; residualIndex++)
            {
                jacobianColumn[residualIndex] = (perturbedResiduals[residualIndex] - residuals[residualIndex]) / step;
            }
        }

        for (var row = 0; row < parameterCount; row++)
        {
            var rowJacobian = jacobianColumns[row];
            for (var residualIndex = 0; residualIndex < residuals.Length; residualIndex++)
            {
                gradient[row] += rowJacobian[residualIndex] * residuals[residualIndex];
            }

            for (var column = row; column < parameterCount; column++)
            {
                var value = 0f;
                var columnJacobian = jacobianColumns[column];
                for (var residualIndex = 0; residualIndex < residuals.Length; residualIndex++)
                {
                    value += rowJacobian[residualIndex] * columnJacobian[residualIndex];
                }

                normalMatrix[row, column] = value;
                normalMatrix[column, row] = value;
            }
        }

        for (var i = 0; i < parameterCount; i++)
        {
            normalMatrix[i, i] += Mathf.Max(damping, 0.000001f);
            gradient[i] = -gradient[i];
        }

        return true;
    }

    private static float[] CreateUnitDelta(int parameterIndex, float value)
    {
        var delta = new float[6];
        delta[parameterIndex] = value;
        return delta;
    }

    private bool TryComputeReprojectionError(
        XrCameraIntrinsics intrinsics,
        in Pose pose,
        Vector3[] localFinderPoints,
        Vector2[] observedFinderPoints,
        out float[] residuals,
        out float cost)
    {
        residuals = new float[localFinderPoints.Length * 2];
        cost = 0f;

        for (var i = 0; i < localFinderPoints.Length; i++)
        {
            var cameraPoint = pose.position + pose.rotation * localFinderPoints[i];
            if (!TryProjectCameraPoint(intrinsics, cameraPoint, out var projectedPoint))
            {
                residuals = null;
                cost = float.PositiveInfinity;
                return false;
            }

            var residualX = projectedPoint.x - observedFinderPoints[i].x;
            var residualY = projectedPoint.y - observedFinderPoints[i].y;
            var residualIndex = i * 2;
            residuals[residualIndex] = residualX;
            residuals[residualIndex + 1] = residualY;
            cost += (residualX * residualX) + (residualY * residualY);
        }

        return true;
    }

    private bool TryProjectCameraPoint(
        XrCameraIntrinsics intrinsics,
        Vector3 cameraPoint,
        out Vector2 projectedPoint)
    {
        projectedPoint = default;

        var depth = invertCameraSpaceForward ? -cameraPoint.z : cameraPoint.z;
        if (depth <= MinAxisMagnitude)
        {
            return false;
        }

        var fx = Mathf.Max(intrinsics.focalLength.X, MinAxisMagnitude);
        var fy = Mathf.Max(intrinsics.focalLength.Y, MinAxisMagnitude);
        projectedPoint = new Vector2(
            (fx * cameraPoint.x / depth) + intrinsics.principalPoint.X,
            intrinsics.principalPoint.Y - (fy * cameraPoint.y / depth));
        return IsFinite(projectedPoint);
    }

    private void PopulateDebugPointsFromPose(QrMarkerDefinition definition, in Pose markerPose)
    {
        debugResultPointPositions.Clear();
        if (definition == null || !definition.HasMetricPoseConfiguration)
        {
            return;
        }

        var localFinderPoints = BuildFinderLocalPoints(definition);
        for (var i = 0; i < localFinderPoints.Length; i++)
        {
            debugResultPointPositions.Add(markerPose.position + markerPose.rotation * localFinderPoints[i]);
        }
    }

    private static Pose ApplyPoseDelta(in Pose pose, float[] delta)
    {
        var rotationDelta = QuaternionFromRotationVector(new Vector3(delta[0], delta[1], delta[2]));
        return new Pose(
            pose.position + new Vector3(delta[3], delta[4], delta[5]),
            NormalizeQuaternion(rotationDelta * pose.rotation));
    }

    private static Quaternion QuaternionFromRotationVector(Vector3 rotationVector)
    {
        var angle = rotationVector.magnitude;
        if (angle <= MinAxisMagnitude)
        {
            return Quaternion.identity;
        }

        return Quaternion.AngleAxis(angle * Mathf.Rad2Deg, rotationVector / angle);
    }

    private static float GetDeltaMagnitude(float[] delta)
    {
        var magnitude = 0f;
        for (var i = 0; i < delta.Length; i++)
        {
            magnitude += delta[i] * delta[i];
        }

        return Mathf.Sqrt(magnitude);
    }

    private static bool TrySolveLinearSystem6x6(float[,] matrix, float[] vector, out float[] solution)
    {
        const int size = 6;
        solution = new float[size];
        var augmented = new double[size, size + 1];

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                augmented[row, column] = matrix[row, column];
            }

            augmented[row, size] = vector[row];
        }

        for (var pivotIndex = 0; pivotIndex < size; pivotIndex++)
        {
            var pivotRow = pivotIndex;
            var pivotMagnitude = Math.Abs(augmented[pivotRow, pivotIndex]);
            for (var row = pivotIndex + 1; row < size; row++)
            {
                var magnitude = Math.Abs(augmented[row, pivotIndex]);
                if (magnitude > pivotMagnitude)
                {
                    pivotMagnitude = magnitude;
                    pivotRow = row;
                }
            }

            if (pivotMagnitude <= 0.000000001d)
            {
                return false;
            }

            if (pivotRow != pivotIndex)
            {
                for (var column = pivotIndex; column <= size; column++)
                {
                    var temp = augmented[pivotIndex, column];
                    augmented[pivotIndex, column] = augmented[pivotRow, column];
                    augmented[pivotRow, column] = temp;
                }
            }

            var pivot = augmented[pivotIndex, pivotIndex];
            for (var row = pivotIndex + 1; row < size; row++)
            {
                var factor = augmented[row, pivotIndex] / pivot;
                if (Math.Abs(factor) <= 0d)
                {
                    continue;
                }

                for (var column = pivotIndex; column <= size; column++)
                {
                    augmented[row, column] -= factor * augmented[pivotIndex, column];
                }
            }
        }

        for (var row = size - 1; row >= 0; row--)
        {
            var value = augmented[row, size];
            for (var column = row + 1; column < size; column++)
            {
                value -= augmented[row, column] * solution[column];
            }

            var diagonal = augmented[row, row];
            if (Math.Abs(diagonal) <= 0.000000001d)
            {
                return false;
            }

            solution[row] = (float)(value / diagonal);
        }

        return true;
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

    private QrPoseResolverDebugView GetDebugView()
    {
        if (debugView == null)
        {
            debugView = new QrPoseResolverDebugView();
        }

        return debugView;
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

    private Pose ResolveCameraLocalPose(in Pose reportedCameraLocalPose)
    {
        return cameraExtrinsicsInterpretation == CameraExtrinsicsInterpretation.InverseOfReportedPose
            ? InvertPose(reportedCameraLocalPose)
            : reportedCameraLocalPose;
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

    private Pose ApplyPoseFilter(Pose resolvedPose)
    {
        if (!hasFilteredPose ||
            (filteredPosition - resolvedPose.position).sqrMagnitude > snapDistance * snapDistance)
        {
            filteredPosition = resolvedPose.position;
            filteredRotation = resolvedPose.rotation;
            hasFilteredPose = true;
            return resolvedPose;
        }

        filteredPosition = Vector3.Lerp(filteredPosition, resolvedPose.position, positionSmoothing);
        filteredRotation = Quaternion.Slerp(filteredRotation, resolvedPose.rotation, rotationSmoothing);
        return new Pose(filteredPosition, filteredRotation);
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

        var validPoints = new Vector2[3];
        var validCount = 0;
        for (var i = 0; i < imageResultPoints.Length && validCount < validPoints.Length; i++)
        {
            if (!IsFinite(imageResultPoints[i]))
            {
                continue;
            }

            validPoints[validCount++] = imageResultPoints[i];
        }

        if (validCount < 3)
        {
            return false;
        }

        var zeroOne = (validPoints[0] - validPoints[1]).sqrMagnitude;
        var oneTwo = (validPoints[1] - validPoints[2]).sqrMagnitude;
        var zeroTwo = (validPoints[0] - validPoints[2]).sqrMagnitude;

        if (oneTwo >= zeroOne && oneTwo >= zeroTwo)
        {
            topLeft = validPoints[0];
            bottomLeft = validPoints[1];
            topRight = validPoints[2];
        }
        else if (zeroTwo >= oneTwo && zeroTwo >= zeroOne)
        {
            topLeft = validPoints[1];
            bottomLeft = validPoints[0];
            topRight = validPoints[2];
        }
        else
        {
            topLeft = validPoints[2];
            bottomLeft = validPoints[0];
            topRight = validPoints[1];
        }

        if (CrossProductZ(bottomLeft, topLeft, topRight) < 0f)
        {
            var temp = bottomLeft;
            bottomLeft = topRight;
            topRight = temp;
        }

        return true;
    }

    private static float CrossProductZ(Vector2 pointA, Vector2 pointB, Vector2 pointC)
    {
        var bX = pointB.x;
        var bY = pointB.y;
        return ((pointC.x - bX) * (pointA.y - bY)) - ((pointC.y - bY) * (pointA.x - bX));
    }

    private static int GetPointCount(Vector2[] points)
    {
        return points?.Length ?? 0;
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

    private readonly struct CameraProjectionData
    {
        public readonly XrCameraIntrinsics Intrinsics;
        public readonly Pose CameraLocalPose;
        public readonly Pose CameraWorldPose;
        public readonly string Mode;
        public readonly string Note;

        public CameraProjectionData(XrCameraIntrinsics intrinsics, in Pose cameraLocalPose)
            : this(intrinsics, cameraLocalPose, default, null, null)
        {
        }

        public CameraProjectionData(
            XrCameraIntrinsics intrinsics,
            in Pose cameraLocalPose,
            in Pose cameraWorldPose,
            string mode,
            string note)
        {
            Intrinsics = intrinsics;
            CameraLocalPose = cameraLocalPose;
            CameraWorldPose = cameraWorldPose;
            Mode = mode;
            Note = note;
        }
    }
}
