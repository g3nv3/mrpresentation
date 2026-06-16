using System;
using UnityEngine;

[Serializable]
public struct PhoneRouteDestination
{
    public double Latitude;
    public double Longitude;
    public string Title;
    public string Address;
    public double Timestamp;
    public int Sequence;

    public bool IsValid => Latitude >= -90d && Latitude <= 90d && Longitude >= -180d && Longitude <= 180d;

    public GeoCoordinate ToGeoCoordinate()
    {
        return new GeoCoordinate(Latitude, Longitude);
    }
}

[Serializable]
public sealed class PhoneRouteDestinationPacket
{
    public const string ExpectedMessageType = "route_destination";
    public const int CurrentProtocolVersion = 1;

    public string MessageType = ExpectedMessageType;
    public int ProtocolVersion = CurrentProtocolVersion;
    public PhoneRouteDestination Destination;

    public bool IsValid()
    {
        return MessageType == ExpectedMessageType &&
               ProtocolVersion == CurrentProtocolVersion &&
               Destination.IsValid;
    }
}
