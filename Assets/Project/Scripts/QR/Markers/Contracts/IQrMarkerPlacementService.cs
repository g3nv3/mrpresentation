public interface IQrMarkerPlacementService
{
    bool TryProcessDetection(in QrDetection detection, out string statusText);
    void ClearCurrentDetection();
}
