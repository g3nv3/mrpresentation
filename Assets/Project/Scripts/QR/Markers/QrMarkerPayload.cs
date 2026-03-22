public readonly struct QrMarkerPayload
{
    public readonly int Version;
    public readonly string MarkerId;

    public QrMarkerPayload(int version, string markerId)
    {
        Version = version;
        MarkerId = markerId;
    }
}
