using System;
using UnityEngine;

[Serializable]
public struct PhoneLocationSample
{
    public string DeviceId;
    public double Latitude;
    public double Longitude;
    public double AltitudeMeters;
    public float HorizontalAccuracyMeters;
    public float VerticalAccuracyMeters;
    public float SpeedMetersPerSecond;
    public float CourseDegrees;
    public double Timestamp;
    public int Sequence;

    public bool IsValid => Latitude >= -90d && Latitude <= 90d && Longitude >= -180d && Longitude <= 180d;

    public override string ToString()
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1} acc={2}m seq={3}",
            Latitude,
            Longitude,
            HorizontalAccuracyMeters,
            Sequence);
    }
}

[Serializable]
public sealed class PhoneLocationPacket
{
    public const string ExpectedMessageType = "phone_location";
    public const int CurrentProtocolVersion = 1;

    public string MessageType = ExpectedMessageType;
    public int ProtocolVersion = CurrentProtocolVersion;
    public PhoneLocationSample Sample;

    public static PhoneLocationPacket Create(PhoneLocationSample sample)
    {
        return new PhoneLocationPacket
        {
            MessageType = ExpectedMessageType,
            ProtocolVersion = CurrentProtocolVersion,
            Sample = sample
        };
    }

    public bool IsValid()
    {
        return MessageType == ExpectedMessageType &&
               ProtocolVersion == CurrentProtocolVersion &&
               Sample.IsValid;
    }
}
