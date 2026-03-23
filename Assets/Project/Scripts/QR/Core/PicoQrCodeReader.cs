using System;
using ZXing;
using ZXing.Common;
using UnityEngine;

public sealed class PicoQrCodeReader
{
    private readonly BarcodeReaderGeneric barcodeReader;
    private float scanIntervalSeconds;

    private float nextScanTime;
    private string lastDecodedText;
    private bool lastDecodeAttempted;
    private byte[] rgbaBuffer;
    private byte[] luminanceBuffer;
    private byte[] rowBuffer;

    public string LastDecodedText => lastDecodedText;
    public bool LastDecodeAttempted => lastDecodeAttempted;
    public event Action<QrDetection> OnRead;

    public float ScanIntervalSeconds
    {
        get => scanIntervalSeconds;
        set => scanIntervalSeconds = Mathf.Max(0f, value);
    }

    public PicoQrCodeReader()
    {
        scanIntervalSeconds = 0.2f;
        barcodeReader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions
            {
                PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                TryHarder = true,
                PureBarcode = false
            }
        };
    }

    public bool TryDecode(
        IntPtr bufferPtr,
        int width,
        int height,
        int stride,
        int bytesPerPixel,
        float currentTimeSeconds,
        long captureTime,
        ulong imageId,
        out QrDetection detection)
    {
        detection = default;

        if (bufferPtr == IntPtr.Zero || width <= 0 || height <= 0 || bytesPerPixel != 4)
        {
            return false;
        }

        if (currentTimeSeconds < nextScanTime)
        {
            lastDecodeAttempted = false;
            return false;
        }

        nextScanTime = currentTimeSeconds + scanIntervalSeconds;
        lastDecodeAttempted = true;

        var packedSize = width * height * bytesPerPixel;
        EnsureRgbaBuffer(packedSize);

        if (stride == width * bytesPerPixel)
        {
            System.Runtime.InteropServices.Marshal.Copy(bufferPtr, rgbaBuffer, 0, packedSize);
        }
        else
        {
            CopyRowByRow(bufferPtr, width, height, stride, bytesPerPixel);
        }

        EnsureLuminanceBuffer(width * height);
        ConvertRgbaToLuminance(width, height);

        var result = barcodeReader.Decode(luminanceBuffer, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        if (result == null || string.IsNullOrWhiteSpace(result.Text))
        {
            return false;
        }

        var imageCenter = TryGetImageCenter(result, out var center);
        detection = new QrDetection(
            result.Text,
            imageCenter,
            center,
            width,
            height,
            captureTime,
            imageId);

        lastDecodedText = result.Text;
        OnRead?.Invoke(detection);
        return true;
    }

    private static bool TryGetImageCenter(Result result, out Vector2 center)
    {
        center = default;
        var resultPoints = result.ResultPoints;
        if (resultPoints == null || resultPoints.Length == 0)
        {
            return false;
        }

        if (TryGetQrFinderCenter(resultPoints, out center))
        {
            return true;
        }

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var count = 0;
        for (var i = 0; i < resultPoints.Length; i++)
        {
            var point = resultPoints[i];
            if (point == null)
            {
                continue;
            }

            minX = Mathf.Min(minX, point.X);
            minY = Mathf.Min(minY, point.Y);
            maxX = Mathf.Max(maxX, point.X);
            maxY = Mathf.Max(maxY, point.Y);
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        // QR readers often return finder-pattern points instead of all 4 corners.
        // Bounding-box center is less biased than averaging sparse points.
        center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        return true;
    }

    private static bool TryGetQrFinderCenter(ResultPoint[] resultPoints, out Vector2 center)
    {
        center = default;

        var validPoints = new ResultPoint[3];
        var validCount = 0;
        for (var i = 0; i < resultPoints.Length && validCount < validPoints.Length; i++)
        {
            var point = resultPoints[i];
            if (point == null)
            {
                continue;
            }

            validPoints[validCount++] = point;
        }

        if (validCount < 3)
        {
            return false;
        }

        ResultPoint.orderBestPatterns(validPoints);

        var bottomLeft = validPoints[0];
        var topRight = validPoints[2];
        center = new Vector2(
            (bottomLeft.X + topRight.X) * 0.5f,
            (bottomLeft.Y + topRight.Y) * 0.5f);
        return true;
    }

    private void CopyRowByRow(IntPtr bufferPtr, int width, int height, int stride, int bytesPerPixel)
    {
        var packedRowSize = width * bytesPerPixel;
        EnsureRowBuffer(stride);

        for (var y = 0; y < height; y++)
        {
            var rowPtr = IntPtr.Add(bufferPtr, y * stride);
            System.Runtime.InteropServices.Marshal.Copy(rowPtr, rowBuffer, 0, stride);
            Buffer.BlockCopy(rowBuffer, 0, rgbaBuffer, y * packedRowSize, packedRowSize);
        }
    }

    private void ConvertRgbaToLuminance(int width, int height)
    {
        var pixelCount = width * height;
        for (var i = 0; i < pixelCount; i++)
        {
            var rgbaIndex = i * 4;
            var r = rgbaBuffer[rgbaIndex];
            var g = rgbaBuffer[rgbaIndex + 1];
            var b = rgbaBuffer[rgbaIndex + 2];
            luminanceBuffer[i] = (byte)((r * 77 + g * 150 + b * 29) >> 8);
        }
    }

    private void EnsureRgbaBuffer(int size)
    {
        if (rgbaBuffer == null || rgbaBuffer.Length != size)
        {
            rgbaBuffer = new byte[size];
        }
    }

    private void EnsureLuminanceBuffer(int size)
    {
        if (luminanceBuffer == null || luminanceBuffer.Length != size)
        {
            luminanceBuffer = new byte[size];
        }
    }

    private void EnsureRowBuffer(int size)
    {
        if (rowBuffer == null || rowBuffer.Length != size)
        {
            rowBuffer = new byte[size];
        }
    }
}
