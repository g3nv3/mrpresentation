using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PicoCameraTextureRenderer
{
    private readonly bool createTargetIfMissing;
    private readonly RawImage previewRawImage;

    private RenderTexture targetTexture;
    private Texture2D frameTexture;
    private Color32[] unpackedPixels;
    private byte[] packedBuffer;
    private byte[] rowBuffer;

    public RenderTexture TargetTexture => targetTexture;

    public PicoCameraTextureRenderer(RenderTexture targetTexture, bool createTargetIfMissing, RawImage previewRawImage)
    {
        this.targetTexture = targetTexture;
        this.createTargetIfMissing = createTargetIfMissing;
        this.previewRawImage = previewRawImage;
    }

    public void Render(in PicoCameraFrame frame, string textureName)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Buffer == IntPtr.Zero)
        {
            return;
        }

        if (frame.BytesPerPixel != 4)
        {
            throw new NotSupportedException($"Unsupported bytesPerPixel: {frame.BytesPerPixel}. Expected RGBA32.");
        }

        EnsureTextures(frame.Width, frame.Height, textureName);

        var packedRowSize = frame.Width * frame.BytesPerPixel;
        if (frame.Stride == packedRowSize)
        {
            EnsurePackedBuffer(packedRowSize * frame.Height);
            System.Runtime.InteropServices.Marshal.Copy(frame.Buffer, packedBuffer, 0, packedBuffer.Length);
            frameTexture.LoadRawTextureData(packedBuffer);
        }
        else
        {
            EnsureUnpackedPixels(frame.Width, frame.Height);
            EnsureRowBuffer(frame.Stride);

            for (var y = 0; y < frame.Height; y++)
            {
                var rowPtr = IntPtr.Add(frame.Buffer, y * frame.Stride);
                System.Runtime.InteropServices.Marshal.Copy(rowPtr, rowBuffer, 0, frame.Stride);

                for (var x = 0; x < frame.Width; x++)
                {
                    var src = x * frame.BytesPerPixel;
                    unpackedPixels[y * frame.Width + x] = new Color32(
                        rowBuffer[src],
                        rowBuffer[src + 1],
                        rowBuffer[src + 2],
                        rowBuffer[src + 3]);
                }
            }

            frameTexture.SetPixelData(unpackedPixels, 0);
        }

        frameTexture.Apply(false, false);
        if (targetTexture != null)
        {
            Graphics.Blit(frameTexture, targetTexture);
        }
    }

    public void Dispose()
    {
        if (frameTexture != null)
        {
            UnityEngine.Object.Destroy(frameTexture);
            frameTexture = null;
        }

        unpackedPixels = null;
        packedBuffer = null;
        rowBuffer = null;
    }

    private void EnsureTextures(int width, int height, string textureName)
    {
        if (frameTexture == null || frameTexture.width != width || frameTexture.height != height)
        {
            if (frameTexture != null)
            {
                UnityEngine.Object.Destroy(frameTexture);
            }

            frameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        if ((targetTexture == null || targetTexture.width != width || targetTexture.height != height) && createTargetIfMissing)
        {
            if (targetTexture != null)
            {
                targetTexture.Release();
            }

            targetTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            targetTexture.Create();
        }

        if (previewRawImage != null && previewRawImage.texture != targetTexture)
        {
            previewRawImage.texture = targetTexture;
        }
    }

    private void EnsureUnpackedPixels(int width, int height)
    {
        var pixelCount = width * height;
        if (unpackedPixels == null || unpackedPixels.Length != pixelCount)
        {
            unpackedPixels = new Color32[pixelCount];
        }
    }

    private void EnsurePackedBuffer(int size)
    {
        if (packedBuffer == null || packedBuffer.Length != size)
        {
            packedBuffer = new byte[size];
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
