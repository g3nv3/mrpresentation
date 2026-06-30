using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneRouteDestinationUdpReceiver : MonoBehaviour
{
    private struct RawPacket
    {
        public string Json;
        public IPEndPoint RemoteEndPoint;
    }

    [Header("UDP")]
    [SerializeField, Range(1, 65535)] private int listenPort = 47778;
    [SerializeField] private bool startOnEnable = true;

    private readonly ConcurrentQueue<RawPacket> _rawPackets = new ConcurrentQueue<RawPacket>();
    private UdpClient _client;
    private Thread _thread;
    private volatile bool _isListening;
    private PhoneRouteDestination _latestDestination;
    private PhoneRouteSelection _latestRouteSelection;
    private IPEndPoint _latestRemoteEndPoint;

    public event Action<PhoneRouteDestination, IPEndPoint> DestinationReceived;
    public event Action<PhoneRouteSelection, IPEndPoint> RouteSelectionReceived;
    public event Action<string> Failed;

    public int ListenPort => listenPort;
    public bool IsListening => _isListening;
    public bool HasDestination { get; private set; }
    public bool HasRouteSelection { get; private set; }
    public PhoneRouteDestination LatestDestination => _latestDestination;
    public PhoneRouteSelection LatestRouteSelection => _latestRouteSelection;
    public IPEndPoint LatestRemoteEndPoint => _latestRemoteEndPoint;

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartListening();
        }
    }

    private void OnDisable()
    {
        StopListening();
    }

    private void Update()
    {
        while (_rawPackets.TryDequeue(out var rawPacket))
        {
            ProcessRawPacket(rawPacket);
        }
    }

    public void StartListening()
    {
        if (_isListening)
        {
            return;
        }

        try
        {
            _client = new UdpClient();
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            _isListening = true;
            _thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "PhoneRouteDestinationUdpReceiver"
            };
            _thread.Start();
        }
        catch (Exception exception)
        {
            StopListening();
            Fail("Failed to start route destination UDP receiver: " + exception.Message);
        }
    }

    public void StopListening()
    {
        _isListening = false;

        if (_client != null)
        {
            _client.Close();
            _client = null;
        }

        if (_thread != null)
        {
            if (!_thread.Join(200))
            {
                _thread.Interrupt();
            }

            _thread = null;
        }
    }

    private void ReceiveLoop()
    {
        while (_isListening)
        {
            try
            {
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var bytes = _client.Receive(ref remoteEndPoint);
                var json = Encoding.UTF8.GetString(bytes);
                _rawPackets.Enqueue(new RawPacket
                {
                    Json = json,
                    RemoteEndPoint = remoteEndPoint
                });
            }
            catch (SocketException)
            {
                if (_isListening)
                {
                    _rawPackets.Enqueue(new RawPacket { Json = string.Empty, RemoteEndPoint = null });
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (ThreadInterruptedException)
            {
                return;
            }
            catch
            {
                if (!_isListening)
                {
                    return;
                }
            }
        }
    }

    private void ProcessRawPacket(RawPacket rawPacket)
    {
        if (string.IsNullOrWhiteSpace(rawPacket.Json))
        {
            return;
        }

        PhoneRouteDestinationPacket packet;
        try
        {
            packet = JsonUtility.FromJson<PhoneRouteDestinationPacket>(rawPacket.Json);
        }
        catch (Exception exception)
        {
            Fail("Failed to parse route destination packet: " + exception.Message);
            return;
        }

        if (packet == null || !packet.IsValid())
        {
            return;
        }

        HasDestination = true;
        _latestDestination = packet.Destination;
        _latestRemoteEndPoint = rawPacket.RemoteEndPoint;

        if (packet.HasRouteSelection)
        {
            HasRouteSelection = true;
            _latestRouteSelection = packet.ToRouteSelection();
            RouteSelectionReceived?.Invoke(_latestRouteSelection, rawPacket.RemoteEndPoint);
        }
        else
        {
            HasRouteSelection = false;
        }

        DestinationReceived?.Invoke(_latestDestination, rawPacket.RemoteEndPoint);
    }

    private void Fail(string error)
    {
        Failed?.Invoke(error);
        Debug.LogWarning(error, this);
    }
}
