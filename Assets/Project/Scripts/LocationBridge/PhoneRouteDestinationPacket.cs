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
public struct PhoneRouteSelection
{
    public PhoneRouteDestination Start;
    public PhoneRouteDestination Destination;

    public bool IsValid => Start.IsValid && Destination.IsValid;
    public bool HasStart => Start.IsValid;
    public bool HasDestination => Destination.IsValid;

    public GeoCoordinate StartCoordinate => Start.ToGeoCoordinate();
    public GeoCoordinate DestinationCoordinate => Destination.ToGeoCoordinate();
}

[Serializable]
public sealed class PhoneRouteDestinationPacket
{
    public const string ExpectedMessageType = "route_destination";
    public const int LegacyProtocolVersion = 1;
    public const int CurrentProtocolVersion = 2;

    public string MessageType = ExpectedMessageType;
    public int ProtocolVersion = CurrentProtocolVersion;
    public PhoneRouteDestination Start;
    public PhoneRouteDestination Destination;

    public bool HasRouteSelection => ProtocolVersion >= CurrentProtocolVersion && Start.IsValid && Destination.IsValid;
    public bool HasLegacyDestination => ProtocolVersion == LegacyProtocolVersion && Destination.IsValid;

    public bool IsValid()
    {
        return MessageType == ExpectedMessageType &&
               (HasRouteSelection || HasLegacyDestination);
    }

    public PhoneRouteSelection ToRouteSelection()
    {
        return new PhoneRouteSelection
        {
            Start = Start,
            Destination = Destination
        };
    }
}
