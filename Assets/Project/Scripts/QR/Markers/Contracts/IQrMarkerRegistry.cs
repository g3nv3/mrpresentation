public interface IQrMarkerRegistry
{
    bool TryGet(string markerId, out QrMarkerDefinition definition);
}
