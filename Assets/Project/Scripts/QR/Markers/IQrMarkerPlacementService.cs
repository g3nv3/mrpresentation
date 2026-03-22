public interface IQrMarkerPlacementService
{
    bool TryPlaceOrUpdate(in QrDetection detection);
}
