public sealed class NullQrMarkerPlacementService : IQrMarkerPlacementService
{
    public bool TryPlaceOrUpdate(in QrDetection detection)
    {
        return false;
    }
}
