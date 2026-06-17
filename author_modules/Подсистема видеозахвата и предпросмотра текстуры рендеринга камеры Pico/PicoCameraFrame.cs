using System;

public readonly struct PicoCameraFrame
{
    public readonly IntPtr Buffer;
    public readonly int Width;
    public readonly int Height;
    public readonly int Stride;
    public readonly int BytesPerPixel;
    public readonly uint BufferSize;
    public readonly long CaptureTime;
    public readonly ulong ImageId;

    public PicoCameraFrame(
        IntPtr buffer,
        int width,
        int height,
        int stride,
        int bytesPerPixel,
        uint bufferSize,
        long captureTime,
        ulong imageId)
    {
        Buffer = buffer;
        Width = width;
        Height = height;
        Stride = stride;
        BytesPerPixel = bytesPerPixel;
        BufferSize = bufferSize;
        CaptureTime = captureTime;
        ImageId = imageId;
    }
}
