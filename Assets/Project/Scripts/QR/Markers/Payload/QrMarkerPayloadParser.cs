using System;

using UnityEngine;

public sealed class QrMarkerPayloadParser : IQrMarkerPayloadParser
{
    public bool TryParse(string rawPayload, out QrMarkerPayload payload)
    {
        payload = default;

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return false;
        }

        var trimmedPayload = rawPayload.Trim();
        if (trimmedPayload.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                var dto = JsonUtility.FromJson<QrMarkerPayloadDto>(trimmedPayload);
                if (dto == null || string.IsNullOrWhiteSpace(dto.id))
                {
                    return false;
                }

                payload = new QrMarkerPayload(dto.v <= 0 ? 1 : dto.v, dto.id.Trim());
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        payload = new QrMarkerPayload(1, trimmedPayload);
        return true;
    }

    [Serializable]
    private sealed class QrMarkerPayloadDto
    {
        public int v = 1;
        public string id;
    }
}
