using UnityEngine;
using Unity.XR.PXR;

public readonly struct QrDetection
{
    public readonly string Text;
    public readonly XrCameraIdPICO CameraId;
    public readonly bool HasImageCenter;
    public readonly Vector2 ImageCenter;
    public readonly Vector2[] ImageResultPoints;
    public readonly int FrameWidth;
    public readonly int FrameHeight;
    public readonly long CaptureTime;
    public readonly ulong ImageId;

    public bool HasImageResultPoints => ImageResultPoints != null && ImageResultPoints.Length > 0;

    public QrDetection(
        string text,
        XrCameraIdPICO cameraId,
        bool hasImageCenter,
        Vector2 imageCenter,
        Vector2[] imageResultPoints,
        int frameWidth,
        int frameHeight,
        long captureTime,
        ulong imageId)
    {
        Text = text;
        CameraId = cameraId;
        HasImageCenter = hasImageCenter;
        ImageCenter = imageCenter;
        ImageResultPoints = imageResultPoints;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        CaptureTime = captureTime;
        ImageId = imageId;
    }
}
