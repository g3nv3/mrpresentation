public interface IQrMarkerPayloadParser
{
    bool TryParse(string rawPayload, out QrMarkerPayload payload);
}
