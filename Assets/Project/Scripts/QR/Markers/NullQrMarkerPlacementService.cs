public sealed class NullQrMarkerPlacementService : IQrMarkerPlacementService
{
    public bool TryProcessDetection(in QrDetection detection, out string statusText)
    {
        statusText = null;
        return false;
    }

    public void ClearCurrentDetection()
    {
    }
}
