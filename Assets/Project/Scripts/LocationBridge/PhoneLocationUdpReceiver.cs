using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneLocationUdpReceiver : MonoBehaviour
{
    private struct RawPacket
    {
        public string Json;
        public IPEndPoint RemoteEndPoint;
    }

    [Header("UDP")]
    [SerializeField, Range(1, 65535)] private int listenPort = 47777;
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private string acceptedDeviceId;

    [Header("State")]
    [SerializeField, Min(0.1f)] private float staleAfterSeconds = 5f;

    private readonly ConcurrentQueue<RawPacket> _rawPackets = new ConcurrentQueue<RawPacket>();
    private UdpClient _client;
    private Thread _thread;
    private volatile bool _isListening;
    private PhoneLocationSample _latestSample;
    private IPEndPoint _latestRemoteEndPoint;
    private float _latestReceiveTime;

    public event Action<PhoneLocationSample, IPEndPoint> LocationReceived;
    public event Action<string> Failed;

    public int ListenPort => listenPort;
    public bool IsListening => _isListening;
    public bool HasLocation { get; private set; }
    public bool IsLatestLocationFresh => HasLocation && Time.unscaledTime - _latestReceiveTime <= staleAfterSeconds;
    public PhoneLocationSample LatestSample => _latestSample;
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

    public void SetAcceptedDeviceId(string deviceId)
    {
        acceptedDeviceId = deviceId;
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
                Name = "PhoneLocationUdpReceiver"
            };
            _thread.Start();
        }
        catch (Exception exception)
        {
            StopListening();
            Fail("Failed to start UDP receiver: " + exception.Message);
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

    public bool TryGetLatestCoordinate(out GeoCoordinate coordinate)
    {
        if (HasLocation)
        {
            coordinate = new GeoCoordinate(_latestSample.Latitude, _latestSample.Longitude);
            return true;
        }

        coordinate = default;
        return false;
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

        PhoneLocationPacket packet;
        try
        {
            packet = JsonUtility.FromJson<PhoneLocationPacket>(rawPacket.Json);
        }
        catch (Exception exception)
        {
            Fail("Failed to parse phone location packet: " + exception.Message);
            return;
        }

        if (packet == null || !packet.IsValid())
        {
            return;
        }

        var sample = packet.Sample;
        if (!string.IsNullOrWhiteSpace(acceptedDeviceId) && sample.DeviceId != acceptedDeviceId)
        {
            return;
        }

        HasLocation = true;
        _latestSample = sample;
        _latestRemoteEndPoint = rawPacket.RemoteEndPoint;
        _latestReceiveTime = Time.unscaledTime;
        LocationReceived?.Invoke(sample, rawPacket.RemoteEndPoint);
    }

    private void Fail(string error)
    {
        Failed?.Invoke(error);
        Debug.LogWarning(error, this);
    }
}
