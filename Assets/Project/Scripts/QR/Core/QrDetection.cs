using UnityEngine;

public readonly struct QrDetection
{
    public readonly string Text;
    public readonly bool HasImageCenter;
    public readonly Vector2 ImageCenter;
    public readonly int FrameWidth;
    public readonly int FrameHeight;
    public readonly long CaptureTime;
    public readonly ulong ImageId;

    public QrDetection(
        string text,
        bool hasImageCenter,
        Vector2 imageCenter,
        int frameWidth,
        int frameHeight,
        long captureTime,
        ulong imageId)
    {
        Text = text;
        HasImageCenter = hasImageCenter;
        ImageCenter = imageCenter;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        CaptureTime = captureTime;
        ImageId = imageId;
    }
}
