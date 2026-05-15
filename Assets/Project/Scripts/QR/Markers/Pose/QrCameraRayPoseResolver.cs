using System;
using System.Collections.Generic;

using UnityEngine;
using Unity.XR.PXR;
using ZXing;

public sealed partial class QrCameraRayPoseResolver : MonoBehaviour, IQrPoseResolver
{
    private enum PoseResolverViewStrategyType
    {
        None = 0,
        Debug = 1,
        CenterTransform = 2
    }

    private enum CameraExtrinsicsInterpretation
    {
        CameraPoseRelativeToDevice = 0,
        InverseOfReportedPose = 1,
        TrackingSpacePose = 2
    }

    private enum ProjectionModelMode
    {
        UseSdkIntrinsics = 0,
        DeriveFromFrameFov = 1
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
    [SerializeField] private ProjectionModelMode projectionModelMode = ProjectionModelMode.DeriveFromFrameFov;
    [SerializeField] private bool ignoreReportedCameraTranslation;
    [SerializeField] private bool ignoreReportedCameraRotation;
    [SerializeField] private bool flipReportedCameraTranslationZ = true;
    [SerializeField] private Vector2 viewportOffset;
    [SerializeField] private bool orientUsingQrResultPoints = true;
    [SerializeField, Range(0f, 1f)] private float positionSmoothing = 0.25f;
    [SerializeField, Range(0f, 1f)] private float rotationSmoothing = 0.25f;
    [SerializeField] private bool adaptSmoothingToDeviceMotion = true;
    [SerializeField, Min(0.001f)] private float deviceMotionLinearThresholdMetersPerSecond = 0.04f;
    [SerializeField, Min(0.1f)] private float deviceMotionAngularThresholdDegreesPerSecond = 20f;
    [SerializeField] private float snapDistance = 0.2f;
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private bool enableMetricPoseRefinement = true;
    [SerializeField, Range(1, 12)] private int metricPoseRefinementIterations = 6;
    [SerializeField, Min(0.0001f)] private float metricPoseRotationStepRadians = 0.01f;
    [SerializeField, Min(0.0001f)] private float metricPoseTranslationStepMeters = 0.005f;
    [SerializeField, Min(0.000001f)] private float metricPoseInitialDamping = 0.001f;
    [SerializeField, Min(0f)] private float metricPoseMaxTranslationDelta = 0.25f;
    [SerializeField, Range(0f, 180f)] private float metricPoseMaxRotationDeltaDegrees = 35f;
    [SerializeField, Min(0f)] private float metricPoseMaxReprojectionErrorPixels = 8f;
    [SerializeField] private bool verbosePoseDiagnostics;
    [SerializeField, Min(0.1f)] private float poseDiagnosticsInterval = 0.5f;
    [SerializeField] private PoseResolverViewStrategyType viewStrategy = PoseResolverViewStrategyType.Debug;
    [SerializeField] private QrPoseResolverDebugView debugView = new QrPoseResolverDebugView();
    [SerializeField] private QrPoseResolverCenterTransformView centerTransformView = new QrPoseResolverCenterTransformView();

    private readonly List<Vector3> debugResultPointPositions = new List<Vector3>(4);
    private bool hasCachedProjectionData;
    private XrCameraIdPICO cachedCameraId;
    private CameraProjectionData cachedProjectionData;
    private string lastDebugStatus = "ray: init";
    private float nextPoseDiagnosticsTime;
    private Vector3 filteredPosition;
    private Quaternion filteredRotation = Quaternion.identity;
    private bool hasFilteredPose;
    private float lastPositionBlend = 1f;
    private float lastRotationBlend = 1f;
    private IQrPoseResolverView activeView;

    public string LastDebugStatus => lastDebugStatus;

    private void Awake()
    {
        InitializeView();
        HideView();
    }

    public bool TryResolvePose(in QrDetection detection, QrMarkerDefinition definition, out Pose pose)
    {
        pose = default;
        lastDebugStatus = "ray: started";

        if (!detection.HasImageCenter || detection.FrameWidth <= 0 || detection.FrameHeight <= 0)
        {
            lastDebugStatus =
                "ray: no image center\n" +
                $"frame={FormatFrameSize(detection.FrameWidth, detection.FrameHeight)}\n" +
                $"points={GetPointCount(detection.ImageResultPoints)}";
            HideView();
            return false;
        }

        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            lastDebugStatus =
                "ray: no target camera\n" +
                $"frame={FormatFrameSize(detection.FrameWidth, detection.FrameHeight)}\n" +
                $"raw={FormatVector2(detection.ImageCenter)}";
            HideView();
            return false;
        }

        if (!TryCreateRayContext(activeCamera, detection, out var context))
        {
            lastDebugStatus = BuildDebugStatus(
                "ray: context failed",
                detection,
                null,
                null,
                null,
                null,
                null);
            HideView();
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
            lastDebugStatus = BuildDebugStatus(
                "ray: build failed",
                detection,
                context,
                null,
                null,
                null,
                null);
            HideView();
            return false;
        }

        if (!TryRaycastWorldRay(ray, out var centerHit))
        {
            debugResultPointPositions.Clear();
            PopulateDebugPointsFromRaycasts(context, detection);
            UpdateView(null);
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
        
        var normalForward = hitNormal.sqrMagnitude >= MinAxisMagnitude
            ? hitNormal.normalized
            : Vector3.forward;
        
        var up = Vector3.ProjectOnPlane(forward, normalForward);
        if (up.sqrMagnitude < MinAxisMagnitude)
        {
            up = Vector3.ProjectOnPlane(Vector3.up, normalForward);
        }

        if (up.sqrMagnitude < MinAxisMagnitude)
        {
            up = Vector3.ProjectOnPlane(Vector3.right, normalForward);
        }

        if (up.sqrMagnitude < MinAxisMagnitude)
        {
            up = Vector3.ProjectOnPlane(Vector3.forward, normalForward);
        }

        up = up.normalized;

        var resolvedPosition = centerHit.point + GetSurfaceOffset(hitNormal);
        var resolvedPose = new Pose(
            resolvedPosition,
            Quaternion.LookRotation(normalForward, up));
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

        resolvedPose = ApplyPoseFilter(context, resolvedPose);

        pose = resolvedPose;

        UpdateView(resolvedPose);
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
        hasFilteredPose = false;
        filteredPosition = default;
        filteredRotation = Quaternion.identity;
        lastDebugStatus = "ray: cleared";
        nextPoseDiagnosticsTime = 0f;
        debugResultPointPositions.Clear();
        HideView();
    }

}
