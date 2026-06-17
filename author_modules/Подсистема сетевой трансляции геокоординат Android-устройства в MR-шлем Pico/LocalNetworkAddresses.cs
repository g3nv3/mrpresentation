using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public static class LocalNetworkAddresses
{
    /// <summary>
    /// Возвращает broadcast-адреса всех активных IPv4-интерфейсов.
    /// Например: 192.168.43.255.
    /// </summary>
    public static List<string> GetBroadcastAddresses()
    {
        var result = new HashSet<string>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType ==
                NetworkInterfaceType.Loopback)
            {
                continue;
            }

            try
            {
                foreach (var unicast in
                         networkInterface.GetIPProperties().UnicastAddresses)
                {
                    var address = unicast.Address;
                    var mask = unicast.IPv4Mask;

                    if (address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (mask == null || IPAddress.IsLoopback(address))
                        continue;

                    var broadcast = CalculateBroadcastAddress(address, mask);

                    // Отбрасываем странные случаи вроде broadcast,
                    // совпадающего с локальным адресом.
                    if (!broadcast.Equals(address))
                    {
                        result.Add(broadcast.ToString());
                    }
                }
            }
            catch
            {
                // Android может не дать данные по части интерфейсов.
            }
        }

        result.Add("255.255.255.255");

        return result
            .OrderBy(address => address == "255.255.255.255" ? 1 : 0)
            .ToList();
    }

    /// <summary>
    /// Возвращает локальные IPv4-адреса устройства.
    /// Например: 192.168.43.1.
    /// </summary>
    public static List<string> GetLocalAddresses()
    {
        var result = new HashSet<string>();

        foreach (NetworkInterface networkInterface in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType ==
                NetworkInterfaceType.Loopback)
            {
                continue;
            }

            try
            {
                foreach (UnicastIPAddressInformation unicast in
                         networkInterface.GetIPProperties().UnicastAddresses)
                {
                    IPAddress address = unicast.Address;

                    if (address.AddressFamily != AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(address))
                    {
                        continue;
                    }

                    result.Add(address.ToString());
                }
            }
            catch
            {
                // Игнорируем недоступные интерфейсы.
            }
        }

        return new List<string>(result);
    }

    /// <summary>
    /// Возвращает локальные и broadcast IPv4-адреса одним списком.
    /// </summary>
    public static List<string> GetAllAddresses()
    {
        var result = new HashSet<string>();

        foreach (string address in GetLocalAddresses())
            result.Add(address);

        foreach (string address in GetBroadcastAddresses())
            result.Add(address);

        return new List<string>(result);
    }

    private static IPAddress CalculateBroadcastAddress(
        IPAddress address,
        IPAddress mask)
    {
        byte[] addressBytes = address.GetAddressBytes();
        byte[] maskBytes = mask.GetAddressBytes();
        byte[] broadcastBytes = new byte[addressBytes.Length];

        for (int i = 0; i < broadcastBytes.Length; i++)
        {
            broadcastBytes[i] = (byte)(
                addressBytes[i] | ~maskBytes[i]
            );
        }

        return new IPAddress(broadcastBytes);
    }
}