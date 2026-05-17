using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

namespace Project.Scripts.UI
{
    [ExecuteAlways]
    [AddComponentMenu("Project/UI/Frosted Glass Image")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class FrostedGlassImage : MonoBehaviour
    {
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

        [SerializeField] private PicoCameraBackgroundBlur backgroundBlur;
        [SerializeField] private Material materialTemplate;
        [Tooltip("Необязательный RawImage-референс для UV. Обычно оставьте пустым: blur будет считать camera texture растянутой на весь экран.")]
        [SerializeField] private RawImage referenceRawImage;

        [Header("Projection")]
        [SerializeField] private bool useWorldCameraProjection = true;
        [SerializeField] private CameraExtrinsicsInterpretation cameraExtrinsicsInterpretation =
            CameraExtrinsicsInterpretation.CameraPoseRelativeToDevice;
        [SerializeField] private ProjectionModelMode projectionModelMode = ProjectionModelMode.DeriveFromFrameFov;
        [SerializeField] private bool invertCameraSpaceForward = true;
        [SerializeField] private bool flipReportedCameraTranslationZ = true;
        [SerializeField] private Vector2 projectionUvOffset;
        [SerializeField] private Vector2 projectionUvScale = Vector2.one;

        [Header("Projection Smoothing")]
        [SerializeField] private bool smoothProjection = true;
        [Tooltip("Время сглаживания pose RGB-камеры. Больше значение = меньше дерганья, но больше запаздывание картинки.")]
        [SerializeField, Range(0.01f, 0.35f)] private float projectionSmoothingTime = 0.08f;
        [Tooltip("При большем скачке позиции сглаживание сбрасывается, чтобы не тянуть картинку после recenter/потери трекинга.")]
        [SerializeField, Range(0f, 0.5f)] private float projectionJumpResetDistance = 0.18f;
        [Tooltip("При большем скачке угла сглаживание сбрасывается, чтобы не тянуть картинку после recenter/потери трекинга.")]
        [SerializeField, Range(0f, 45f)] private float projectionJumpResetAngle = 18f;

        [Header("Acrylic")]
        [SerializeField] private Color tint = new(0.95686275f, 0.95686275f, 0.9411765f, 0f);
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        [SerializeField, Range(0f, 2f)] private float saturation = 0.75f;
        [SerializeField, Range(0f, 2f)] private float brightness = 1f;
        [SerializeField] private bool flipY;

        private static readonly int BlurTextureId = Shader.PropertyToID("_BlurTex");
        private static readonly int TintId = Shader.PropertyToID("_FrostedTint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int FlipYId = Shader.PropertyToID("_FlipY");
        private static readonly int UseReferenceScreenRectId = Shader.PropertyToID("_UseReferenceScreenRect");
        private static readonly int ReferenceScreenRectId = Shader.PropertyToID("_ReferenceScreenRect");
        private static readonly int ReferenceUvRectId = Shader.PropertyToID("_ReferenceUvRect");
        private static readonly int UseWorldProjectionId = Shader.PropertyToID("_UseWorldProjection");
        private static readonly int WorldToPicoCameraId = Shader.PropertyToID("_WorldToPicoCamera");
        private static readonly int PicoProjectionId = Shader.PropertyToID("_PicoProjection");
        private static readonly int PicoFrameSizeId = Shader.PropertyToID("_PicoFrameSize");
        private static readonly int ProjectionDepthSignId = Shader.PropertyToID("_ProjectionDepthSign");
        private static readonly int ProjectionUvTransformId = Shader.PropertyToID("_ProjectionUvTransform");
        private const string FrostedShaderName = "Project/UI/FrostedGlassImage";
        private const float MinAxisMagnitude = 0.0001f;

        private Image image;
        private Material previousMaterial;
        private Material runtimeMaterial;
        private readonly Vector3[] referenceWorldCorners = new Vector3[4];
        private bool hasSmoothedCameraPose;
        private Pose smoothedCameraPose;

        private void OnEnable()
        {
            image = GetComponent<Image>();
            previousMaterial = image.material;
            EnsureMaterial();
            ApplyProperties();
        }

        private void LateUpdate()
        {
            ApplyProperties();
        }

        private void OnDisable()
        {
            if (image != null && image.material == runtimeMaterial)
            {
                image.material = previousMaterial;
            }

            if (runtimeMaterial != null)
            {
                DestroyUnityObject(runtimeMaterial);
                runtimeMaterial = null;
            }

            hasSmoothedCameraPose = false;
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            image = GetComponent<Image>();
            EnsureMaterial();
            ApplyProperties();
        }

        private void EnsureMaterial()
        {
            if (image == null || runtimeMaterial != null)
            {
                return;
            }

            var template = materialTemplate != null ? materialTemplate : previousMaterial;
            if (template != null && template.shader != null && template.shader.name == FrostedShaderName)
            {
                runtimeMaterial = new Material(template)
                {
                    name = $"{name} Frosted Glass Material",
                    hideFlags = HideFlags.HideAndDontSave
                };

                image.material = runtimeMaterial;
                return;
            }

            var shader = Shader.Find(FrostedShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader '{FrostedShaderName}' was not found.", this);
                return;
            }

            runtimeMaterial = new Material(shader)
            {
                name = $"{name} Frosted Glass Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            image.material = runtimeMaterial;
        }

        private void ApplyProperties()
        {
            if (runtimeMaterial == null)
            {
                return;
            }

            if (backgroundBlur == null)
            {
                backgroundBlur = FindFirstObjectByType<PicoCameraBackgroundBlur>();
            }

            var blurredTexture = backgroundBlur != null ? backgroundBlur.BlurredTexture : null;
            if (blurredTexture != null)
            {
                runtimeMaterial.SetTexture(BlurTextureId, blurredTexture);
            }

            runtimeMaterial.SetColor(TintId, tint);
            runtimeMaterial.SetFloat(OpacityId, opacity);
            runtimeMaterial.SetFloat(SaturationId, saturation);
            runtimeMaterial.SetFloat(BrightnessId, brightness);
            runtimeMaterial.SetFloat(FlipYId, flipY ? 1f : 0f);
            ApplyWorldCameraProjection();
            ApplyReferenceMapping();
        }

        private void ApplyWorldCameraProjection()
        {
            if (!useWorldCameraProjection ||
                backgroundBlur == null ||
                !TryGetWorldCameraProjection(backgroundBlur.SourceTexture, out var worldToCamera, out var projection, out var frameSize))
            {
                hasSmoothedCameraPose = false;
                runtimeMaterial.SetFloat(UseWorldProjectionId, 0f);
                return;
            }

            runtimeMaterial.SetFloat(UseWorldProjectionId, 1f);
            runtimeMaterial.SetMatrix(WorldToPicoCameraId, worldToCamera);
            runtimeMaterial.SetVector(PicoProjectionId, projection);
            runtimeMaterial.SetVector(PicoFrameSizeId, frameSize);
            runtimeMaterial.SetFloat(ProjectionDepthSignId, invertCameraSpaceForward ? -1f : 1f);
            runtimeMaterial.SetVector(
                ProjectionUvTransformId,
                new Vector4(projectionUvScale.x, projectionUvScale.y, projectionUvOffset.x, projectionUvOffset.y));
        }

        private bool TryGetWorldCameraProjection(Texture sourceTexture, out Matrix4x4 worldToCamera, out Vector4 projection, out Vector4 frameSize)
        {
            worldToCamera = Matrix4x4.identity;
            projection = default;
            frameSize = default;

            var activeCamera = Camera.main;
            if (activeCamera == null || sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            {
                return false;
            }

            var cameraSource = FindFirstObjectByType<PicoCameraRenderTextureSource>();
            if (cameraSource == null)
            {
                return false;
            }

            var cameraId = cameraSource.CurrentCameraId;
            if (PXR_CameraImage.GetCameraIntrinsics(cameraId, out var intrinsics) != PxrResult.SUCCESS ||
                PXR_CameraImage.GetCameraExtrinsics(cameraId, out var extrinsics) != PxrResult.SUCCESS)
            {
                return false;
            }

            if (!TryBuildProjectionModel(intrinsics, sourceTexture.width, sourceTexture.height, out projection))
            {
                return false;
            }

            var trackingOrigin = activeCamera.transform.parent;
            var trackingOriginWorldPose = trackingOrigin != null
                ? new Pose(trackingOrigin.position, trackingOrigin.rotation)
                : Pose.identity;
            var headWorldPose = TryGetCurrentDeviceWorldPose(trackingOriginWorldPose, out var devicePose)
                ? devicePose
                : new Pose(activeCamera.transform.position, activeCamera.transform.rotation);
            var reportedCameraPose = new Pose(
                new Vector3(extrinsics.pose.Position.X, extrinsics.pose.Position.Y, extrinsics.pose.Position.Z),
                NormalizeQuaternion(new Quaternion(
                    extrinsics.pose.Orientation.X,
                    extrinsics.pose.Orientation.Y,
                    extrinsics.pose.Orientation.Z,
                    extrinsics.pose.Orientation.W)));
            var cameraWorldPose = ResolveCameraWorldPose(
                headWorldPose,
                trackingOriginWorldPose,
                AdjustReportedCameraLocalPose(reportedCameraPose));
            cameraWorldPose = SmoothCameraPose(cameraWorldPose);

            worldToCamera = Matrix4x4.TRS(cameraWorldPose.position, cameraWorldPose.rotation, Vector3.one).inverse;
            frameSize = new Vector4(sourceTexture.width, sourceTexture.height, 1f / sourceTexture.width, 1f / sourceTexture.height);
            return true;
        }

        private Pose SmoothCameraPose(Pose cameraWorldPose)
        {
            if (!Application.isPlaying || !smoothProjection)
            {
                hasSmoothedCameraPose = false;
                return cameraWorldPose;
            }

            if (!hasSmoothedCameraPose || ShouldResetProjectionSmoothing(cameraWorldPose))
            {
                smoothedCameraPose = cameraWorldPose;
                hasSmoothedCameraPose = true;
                return smoothedCameraPose;
            }

            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            var smoothingTime = Mathf.Max(projectionSmoothingTime, 0.0001f);
            var t = 1f - Mathf.Exp(-deltaTime / smoothingTime);
            smoothedCameraPose = new Pose(
                Vector3.Lerp(smoothedCameraPose.position, cameraWorldPose.position, t),
                Quaternion.Slerp(smoothedCameraPose.rotation, cameraWorldPose.rotation, t));
            return smoothedCameraPose;
        }

        private bool ShouldResetProjectionSmoothing(in Pose cameraWorldPose)
        {
            var positionJumped = projectionJumpResetDistance > 0f &&
                                 Vector3.Distance(smoothedCameraPose.position, cameraWorldPose.position) >
                                 projectionJumpResetDistance;
            var rotationJumped = projectionJumpResetAngle > 0f &&
                                 Quaternion.Angle(smoothedCameraPose.rotation, cameraWorldPose.rotation) >
                                 projectionJumpResetAngle;
            return positionJumped || rotationJumped;
        }

        private bool TryBuildProjectionModel(XrCameraIntrinsics intrinsics, int frameWidth, int frameHeight, out Vector4 projection)
        {
            projection = default;
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
                    Mathf.Abs(halfHorizontalTangent) > MinAxisMagnitude &&
                    Mathf.Abs(halfVerticalTangent) > MinAxisMagnitude)
                {
                    projection = new Vector4(
                        frameWidth * 0.5f / halfHorizontalTangent,
                        frameHeight * 0.5f / halfVerticalTangent,
                        frameWidth * 0.5f,
                        frameHeight * 0.5f);
                    return true;
                }
            }

            projection = new Vector4(
                intrinsics.focalLength.X,
                intrinsics.focalLength.Y,
                intrinsics.principalPoint.X,
                intrinsics.principalPoint.Y);
            return projection.x > MinAxisMagnitude && projection.y > MinAxisMagnitude;
        }

        private Pose AdjustReportedCameraLocalPose(in Pose reportedCameraPose)
        {
            var position = reportedCameraPose.position;
            if (flipReportedCameraTranslationZ)
            {
                position.z = -position.z;
            }

            return new Pose(position, reportedCameraPose.rotation);
        }

        private Pose ResolveCameraWorldPose(in Pose headWorldPose, in Pose trackingOriginWorldPose, in Pose reportedCameraPose)
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

        private static bool TryGetCurrentDeviceWorldPose(in Pose trackingOriginWorldPose, out Pose deviceWorldPose)
        {
            deviceWorldPose = default;
            var sensorFrameIndex = 0;
            var sensorState = default(PxrSensorState2);
            if (PXR_System.GetPredictedMainSensorStateNew(ref sensorState, ref sensorFrameIndex) != 0)
            {
                return false;
            }

            deviceWorldPose = ConvertPluginPose(sensorState.pose).GetTransformedBy(trackingOriginWorldPose);
            return true;
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

        private static Pose TransformPose(in Pose parentPose, in Pose localPose)
        {
            return new Pose(
                parentPose.position + parentPose.rotation * localPose.position,
                parentPose.rotation * localPose.rotation);
        }

        private static Pose InvertPose(in Pose pose)
        {
            var inverseRotation = Quaternion.Inverse(pose.rotation);
            return new Pose(-(inverseRotation * pose.position), inverseRotation);
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

        private void ApplyReferenceMapping()
        {
            var rawImage = referenceRawImage;
            var rectTransform = rawImage != null ? rawImage.rectTransform : null;
            if (rectTransform == null)
            {
                runtimeMaterial.SetFloat(UseReferenceScreenRectId, 0f);
                runtimeMaterial.SetVector(ReferenceScreenRectId, new Vector4(0f, 0f, 1f, 1f));
                runtimeMaterial.SetVector(ReferenceUvRectId, new Vector4(0f, 0f, 1f, 1f));
                return;
            }

            var canvas = rawImage.canvas;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            rectTransform.GetWorldCorners(referenceWorldCorners);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < referenceWorldCorners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, referenceWorldCorners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            if (Screen.width <= 0 || Screen.height <= 0 || max.x <= min.x || max.y <= min.y)
            {
                runtimeMaterial.SetFloat(UseReferenceScreenRectId, 0f);
                return;
            }

            var uvRect = rawImage.uvRect;
            runtimeMaterial.SetFloat(UseReferenceScreenRectId, 1f);
            runtimeMaterial.SetVector(
                ReferenceScreenRectId,
                new Vector4(
                    min.x / Screen.width,
                    min.y / Screen.height,
                    (max.x - min.x) / Screen.width,
                    (max.y - min.y) / Screen.height));
            runtimeMaterial.SetVector(ReferenceUvRectId, new Vector4(uvRect.x, uvRect.y, uvRect.width, uvRect.height));
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
