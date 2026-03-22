using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

public sealed class PicoCameraRenderTextureSource : MonoBehaviour
{
    [Header("Output")]
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private bool createTargetIfMissing = true;
    [SerializeField] private RawImage previewRawImage;
    [SerializeField] private TMP_Text decodedQrTextLabel;

    [Header("Preferred Camera Config")]
    [SerializeField] private XrCameraIdPICO preferredCameraId = XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO;
    [SerializeField] private Vector2Int preferredResolution = new Vector2Int(640, 480);
    [SerializeField] private XrCameraImageFpsPICO preferredFps = XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO;
    [SerializeField] private bool autoStartOnEnable = true;

    [Header("QR")]
    [SerializeField] private bool enableQrDetection;
    [SerializeField] private float qrScanIntervalSeconds = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    private CancellationTokenSource initializationCts;
    private PicoCameraTextureRenderer textureRenderer;
    private long lastCaptureTime;
    private bool isInitialized;
    private bool isInitializing;
    private bool isWaitingForPermission;
    private bool captureStarted;
    private bool hasLoggedAcquireFailure;
    private PxrResult lastAcquireResult = PxrResult.Unknown;
    private string lastDecodedQrText;
    private PicoQrCodeReader qrCodeReader;
    private IQrMarkerPlacementService qrMarkerPlacementService;
    private float nextQrMissLogTime;

    private XrCameraIdPICO activeCameraId;
    private Vector2Int activeResolution;
    private XrCameraImageFpsPICO activeFps;
    private const XrCameraImageFormatPICO ActiveFormat = XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO;
    private const XrCameraDataTransferTypePICO ActiveTransferType = XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO;
    private const XrCameraModelPICO ActiveModel = XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO;

    public RenderTexture TargetTexture => textureRenderer?.TargetTexture ?? targetTexture;
    public bool IsInitialized => isInitialized;
    public PxrResult LastAcquireResult => lastAcquireResult;
    public string LastDecodedQrText => lastDecodedQrText;

    [VContainer.Inject]
    public void Construct(PicoQrCodeReader injectedQrCodeReader, IQrMarkerPlacementService injectedQrMarkerPlacementService)
    {
        qrCodeReader = injectedQrCodeReader;
        qrMarkerPlacementService = injectedQrMarkerPlacementService;

        if (qrCodeReader != null)
        {
            qrCodeReader.ScanIntervalSeconds = qrScanIntervalSeconds;
        }
    }

    private void OnEnable()
    {
        if (decodedQrTextLabel != null && string.IsNullOrWhiteSpace(decodedQrTextLabel.text))
        {
            decodedQrTextLabel.text = enableQrDetection ? "No QR detected" : string.Empty;
        }

        if (autoStartOnEnable)
        {
            _ = InitializeAsync();
        }
    }

    private void Update()
    {
        // После системного permission dialog повторяем инициализацию автоматически.
        if (isWaitingForPermission && Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            isWaitingForPermission = false;
            _ = InitializeAsync();
        }

        if (!isInitialized)
        {
            return;
        }

        TryUpdateFrame();
    }

    private void OnDisable()
    {
        Shutdown();
    }

    public async Task InitializeAsync()
    {
        if (isInitialized || isInitializing)
        {
            LogVerbose($"InitializeAsync skipped. isInitialized={isInitialized}, isInitializing={isInitializing}");
            return;
        }

        if (!EnsureCameraPermission())
        {
            isWaitingForPermission = true;
            LogVerbose("Camera permission requested.");
            return;
        }

        isWaitingForPermission = false;
        isInitializing = true;
        initializationCts = new CancellationTokenSource();

        try
        {
            textureRenderer ??= new PicoCameraTextureRenderer(targetTexture, createTargetIfMissing, previewRawImage);

            if (enableQrDetection)
            {
                if (qrCodeReader == null)
                {
                    LogError("QR reader dependency is not configured.");
                    return;
                }

                qrCodeReader.ScanIntervalSeconds = qrScanIntervalSeconds;
                LogVerbose($"QR reader ready. Interval={qrScanIntervalSeconds:0.###}s");
            }

            if (!TryResolveSupportedConfiguration(out activeCameraId, out activeResolution, out activeFps))
            {
                LogError("No supported Pico camera configuration was found.");
                return;
            }

            // Официальный Pico flow: create device -> create capture session -> begin capture.
            LogVerbose($"Creating camera device for {activeCameraId}.");
            var createDeviceResult = await PXR_CameraImage.CreateCameraDeviceAsync(activeCameraId, initializationCts.Token);
            if (createDeviceResult != PxrResult.SUCCESS)
            {
                LogError($"CreateCameraDeviceAsync failed: {createDeviceResult}");
                return;
            }
            LogVerbose($"CreateCameraDeviceAsync succeeded: {createDeviceResult}");

            LogVerbose($"Creating capture session. Resolution={activeResolution.x}x{activeResolution.y}, fps={activeFps}, format={ActiveFormat}, transfer={ActiveTransferType}, model={ActiveModel}");
            var createSessionResult = await PXR_CameraImage.CreateCameraCaptureSessionAsync(
                activeCameraId,
                activeResolution.x,
                activeResolution.y,
                activeFps,
                ActiveFormat,
                ActiveTransferType,
                ActiveModel,
                initializationCts.Token);

            if (createSessionResult != PxrResult.SUCCESS)
            {
                LogError($"CreateCameraCaptureSessionAsync failed: {createSessionResult}");
                CleanupDeviceOnly();
                return;
            }
            LogVerbose($"CreateCameraCaptureSessionAsync succeeded: {createSessionResult}");

            var beginCaptureResult = PXR_CameraImage.BeginCameraCapture(activeCameraId);
            if (beginCaptureResult != PxrResult.SUCCESS)
            {
                LogError($"BeginCameraCapture failed: {beginCaptureResult}");
                CleanupSessionAndDevice();
                return;
            }
            LogVerbose($"BeginCameraCapture succeeded: {beginCaptureResult}");

            captureStarted = true;
            lastCaptureTime = 0;
            isInitialized = true;
            hasLoggedAcquireFailure = false;

            LogVerbose($"Initialized camera={activeCameraId}, resolution={activeResolution.x}x{activeResolution.y}, fps={activeFps}.");
        }
        catch (OperationCanceledException)
        {
            LogVerbose("Camera initialization cancelled.");
        }
        finally
        {
            isInitializing = false;
        }
    }

    public void Shutdown()
    {
        initializationCts?.Cancel();
        initializationCts?.Dispose();
        initializationCts = null;

        if (captureStarted)
        {
            var endCaptureResult = PXR_CameraImage.EndCameraCapture(activeCameraId);
            if (endCaptureResult != PxrResult.SUCCESS)
            {
                LogVerbose($"EndCameraCapture returned: {endCaptureResult}");
            }
        }

        captureStarted = false;
        isInitialized = false;
        isInitializing = false;
        isWaitingForPermission = false;
        lastCaptureTime = 0;
        lastAcquireResult = PxrResult.Unknown;
        hasLoggedAcquireFailure = false;

        CleanupSessionAndDevice();
        textureRenderer?.Dispose();
        textureRenderer = null;
        nextQrMissLogTime = 0f;

        if (decodedQrTextLabel != null)
        {
            decodedQrTextLabel.text = string.Empty;
        }
    }

    private bool TryResolveSupportedConfiguration(
        out XrCameraIdPICO cameraId,
        out Vector2Int resolution,
        out XrCameraImageFpsPICO fps)
    {
        cameraId = preferredCameraId;
        resolution = preferredResolution;
        fps = preferredFps;

        var cameraResult = PXR_CameraImage.GetAvailableCameras(out var availableCameras);
        if (cameraResult != PxrResult.SUCCESS || availableCameras == null || availableCameras.Length == 0)
        {
            LogError($"GetAvailableCameras failed: {cameraResult}");
            return false;
        }

        LogVerbose($"Available cameras: {string.Join(", ", availableCameras.Select(c => c.ToString()))}");

        cameraId = availableCameras.Contains(preferredCameraId) ? preferredCameraId : availableCameras[0];
        LogVerbose($"Selected camera: {cameraId}. Preferred={preferredCameraId}");

        if (!IsCapabilitySupported(cameraId, ActiveFormat, out var formatError))
        {
            LogError(formatError);
            return false;
        }

        if (!IsCapabilitySupported(cameraId, ActiveTransferType, out var transferError))
        {
            LogError(transferError);
            return false;
        }

        if (!IsCapabilitySupported(cameraId, ActiveModel, out var modelError))
        {
            LogError(modelError);
            return false;
        }

        if (!TryResolveResolution(cameraId, out resolution))
        {
            return false;
        }

        if (!TryResolveFps(cameraId, out fps))
        {
            return false;
        }

        return true;
    }

    private bool TryResolveResolution(XrCameraIdPICO cameraId, out Vector2Int resolution)
    {
        resolution = preferredResolution;

        var result = PXR_CameraImage.GetCameraImageResolutionCapability(cameraId, out var resolutions);
        if (result != PxrResult.SUCCESS || resolutions == null || resolutions.Length == 0)
        {
            LogError($"GetCameraImageResolutionCapability failed: {result}");
            return false;
        }

        LogVerbose($"Supported resolutions for {cameraId}: {string.Join(", ", resolutions.Select(r => $"{r.width}x{r.height}"))}");

        foreach (var item in resolutions)
        {
            if (item.width == preferredResolution.x && item.height == preferredResolution.y)
            {
                resolution = new Vector2Int(item.width, item.height);
                return true;
            }
        }

        // Если предпочтительное разрешение не поддерживается, берём первый вариант из capability list.
        resolution = new Vector2Int(resolutions[0].width, resolutions[0].height);
        LogVerbose($"Preferred resolution is unsupported. Falling back to {resolution.x}x{resolution.y}.");
        return true;
    }

    private bool TryResolveFps(XrCameraIdPICO cameraId, out XrCameraImageFpsPICO fps)
    {
        fps = preferredFps;

        var result = PXR_CameraImage.GetCameraImageFpsCapability(cameraId, out var supportedFps);
        if (result != PxrResult.SUCCESS || supportedFps == null || supportedFps.Length == 0)
        {
            LogError($"GetCameraImageFpsCapability failed: {result}");
            return false;
        }

        LogVerbose($"Supported FPS for {cameraId}: {string.Join(", ", supportedFps.Select(v => v.ToString()))}");

        fps = supportedFps.Contains(preferredFps) ? preferredFps : supportedFps[0];
        if (fps != preferredFps)
        {
            LogVerbose($"Preferred FPS is unsupported. Falling back to {fps}.");
        }

        return true;
    }

    private bool IsCapabilitySupported(XrCameraIdPICO cameraId, XrCameraImageFormatPICO format, out string error)
    {
        error = null;

        var result = PXR_CameraImage.GetCameraImageFormatCapability(cameraId, out var formats);
        if (result != PxrResult.SUCCESS || formats == null || formats.Length == 0)
        {
            error = $"GetCameraImageFormatCapability failed: {result}";
            return false;
        }

        LogVerbose($"Supported formats for {cameraId}: {string.Join(", ", formats.Select(v => v.ToString()))}");

        if (!formats.Contains(format))
        {
            error = $"Camera {cameraId} does not support format {format}.";
            return false;
        }

        return true;
    }

    private bool IsCapabilitySupported(XrCameraIdPICO cameraId, XrCameraDataTransferTypePICO transferType, out string error)
    {
        error = null;

        var result = PXR_CameraImage.GetCameraDataTransferTypeCapability(cameraId, out var transferTypes);
        if (result != PxrResult.SUCCESS || transferTypes == null || transferTypes.Length == 0)
        {
            error = $"GetCameraDataTransferTypeCapability failed: {result}";
            return false;
        }

        LogVerbose($"Supported transfer types for {cameraId}: {string.Join(", ", transferTypes.Select(v => v.ToString()))}");

        if (!transferTypes.Contains(transferType))
        {
            error = $"Camera {cameraId} does not support transfer type {transferType}.";
            return false;
        }

        return true;
    }

    private bool IsCapabilitySupported(XrCameraIdPICO cameraId, XrCameraModelPICO model, out string error)
    {
        error = null;

        var result = PXR_CameraImage.GetCameraCameraModelCapability(cameraId, out var models);
        if (result != PxrResult.SUCCESS || models == null || models.Length == 0)
        {
            error = $"GetCameraCameraModelCapability failed: {result}";
            return false;
        }

        LogVerbose($"Supported camera models for {cameraId}: {string.Join(", ", models.Select(v => v.ToString()))}");

        if (!models.Contains(model))
        {
            error = $"Camera {cameraId} does not support model {model}.";
            return false;
        }

        return true;
    }

    private void TryUpdateFrame()
    {
        // По документации нужно передавать lastCaptureTime, чтобы получать только новый кадр.
        var acquireResult = PXR_CameraImage.AcquireCameraImage(activeCameraId, lastCaptureTime, out var imageId, out var captureTime);
        lastAcquireResult = acquireResult;
        if (acquireResult != PxrResult.SUCCESS)
        {
            if (!hasLoggedAcquireFailure)
            {
                hasLoggedAcquireFailure = true;
                LogVerbose($"AcquireCameraImage returned: {acquireResult}");
            }
            return;
        }

        hasLoggedAcquireFailure = false;
        LogVerbose($"AcquireCameraImage succeeded. captureTime={captureTime}, imageId={imageId}");

        try
        {
            var imageDataResult = PXR_CameraImage.GetCameraImageData(activeCameraId, imageId, out var rawBuffer);
            if (imageDataResult != PxrResult.SUCCESS)
            {
                LogVerbose($"GetCameraImageData failed: {imageDataResult}");
                return;
            }

            LogVerbose($"GetCameraImageData succeeded. width={rawBuffer.width}, height={rawBuffer.height}, stride={rawBuffer.stride}, bytesPerPixel={rawBuffer.bytesPerPixel}, bufferSize={rawBuffer.bufferSize}");

            var frame = new PicoCameraFrame(
                rawBuffer.buffer,
                (int)rawBuffer.width,
                (int)rawBuffer.height,
                (int)rawBuffer.stride,
                (int)rawBuffer.bytesPerPixel,
                rawBuffer.bufferSize,
                captureTime,
                imageId);

            ProcessFrame(frame);
            lastCaptureTime = captureTime;
        }
        finally
        {
            // Освобождение image handle обязательно после чтения raw buffer.
            var releaseResult = PXR_CameraImage.ReleaseCameraImage(activeCameraId, imageId);
            if (releaseResult != PxrResult.SUCCESS)
            {
                LogVerbose($"ReleaseCameraImage failed: {releaseResult}");
            }
        }
    }

    private void ProcessFrame(in PicoCameraFrame frame)
    {
        ProcessQrFrame(frame);

        if (textureRenderer == null)
        {
            return;
        }

        try
        {
            textureRenderer.Render(frame, $"{nameof(PicoCameraRenderTextureSource)}_{activeCameraId}");
            targetTexture = textureRenderer.TargetTexture;
        }
        catch (Exception exception)
        {
            LogError($"Texture rendering failed: {exception.Message}");
        }
    }

    private void ProcessQrFrame(in PicoCameraFrame frame)
    {
        if (qrCodeReader == null)
        {
            return;
        }

        if (qrCodeReader.TryDecode(
                frame.Buffer,
                frame.Width,
                frame.Height,
                frame.Stride,
                frame.BytesPerPixel,
                Time.unscaledTime,
                frame.CaptureTime,
                frame.ImageId,
                out var detection))
        {
            lastDecodedQrText = detection.Text;
            if (decodedQrTextLabel != null)
            {
                decodedQrTextLabel.text = detection.Text;
            }
            var placementSucceeded = qrMarkerPlacementService != null && qrMarkerPlacementService.TryPlaceOrUpdate(detection);
            LogVerbose($"QR detected: {detection.Text}. Placement={placementSucceeded}");
            return;
        }

        if (decodedQrTextLabel != null && enableQrDetection && string.IsNullOrEmpty(lastDecodedQrText))
        {
            decodedQrTextLabel.text = "No QR detected";
        }

        if (qrCodeReader.LastDecodeAttempted && Time.unscaledTime >= nextQrMissLogTime)
        {
            nextQrMissLogTime = Time.unscaledTime + 1f;
            LogVerbose("QR decode attempt: no result.");
        }
    }

    private static bool EnsureCameraPermission()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            return false;
        }

        return true;
    }

    private void CleanupDeviceOnly()
    {
        var destroyDeviceResult = PXR_CameraImage.DestroyCameraDevice(activeCameraId);
        if (destroyDeviceResult != PxrResult.SUCCESS)
        {
            LogVerbose($"DestroyCameraDevice returned: {destroyDeviceResult}");
        }
    }

    private void CleanupSessionAndDevice()
    {
        var destroySessionResult = PXR_CameraImage.DestroyCameraCaptureSession(activeCameraId);
        if (destroySessionResult != PxrResult.SUCCESS)
        {
            LogVerbose($"DestroyCameraCaptureSession returned: {destroySessionResult}");
        }

        var destroyDeviceResult = PXR_CameraImage.DestroyCameraDevice(activeCameraId);
        if (destroyDeviceResult != PxrResult.SUCCESS)
        {
            LogVerbose($"DestroyCameraDevice returned: {destroyDeviceResult}");
        }
    }

    private void LogVerbose(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[{nameof(PicoCameraRenderTextureSource)}] {message}", this);
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[{nameof(PicoCameraRenderTextureSource)}] {message}", this);
    }
}
