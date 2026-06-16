using System;
using UnityEngine;

public static class YandexMiniMapProjection
{
    public const int TileSize = 256;
    private const double MaxLatitude = 85.05112878d;

    public static double GetMapSize(int zoom)
    {
        return TileSize * Math.Pow(2d, Mathf.Clamp(zoom, 0, 30));
    }

    public static Vector2 GeoToWorldPixel(GeoCoordinate coordinate, int zoom)
    {
        var latitude = Math.Max(-MaxLatitude, Math.Min(MaxLatitude, coordinate.Latitude));
        var longitude = coordinate.Longitude;
        var sinLatitude = Math.Sin(latitude * Mathf.Deg2Rad);
        var mapSize = GetMapSize(zoom);

        var x = (longitude + 180d) / 360d * mapSize;
        var y = (0.5d - Math.Log((1d + sinLatitude) / (1d - sinLatitude)) / (4d * Math.PI)) * mapSize;

        return new Vector2((float)x, (float)y);
    }

    public static Vector2 WorldPixelToUiOffset(Vector2 pointWorldPixel, Vector2 centerWorldPixel, float uiScale)
    {
        var delta = pointWorldPixel - centerWorldPixel;
        return new Vector2(delta.x, -delta.y) * uiScale;
    }

    public static int GetTileCount(int zoom)
    {
        return 1 << Mathf.Clamp(zoom, 0, 30);
    }

    public static int WrapTileX(int x, int zoom)
    {
        var tileCount = GetTileCount(zoom);
        if (tileCount <= 0)
        {
            return 0;
        }

        x %= tileCount;
        return x < 0 ? x + tileCount : x;
    }

    public static int ClampTileY(int y, int zoom)
    {
        var tileCount = GetTileCount(zoom);
        return Mathf.Clamp(y, 0, tileCount - 1);
    }
}
