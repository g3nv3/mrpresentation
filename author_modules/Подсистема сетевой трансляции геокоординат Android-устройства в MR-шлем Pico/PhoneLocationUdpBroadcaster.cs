using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneLocationUdpBroadcaster : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PhoneGpsLocationProvider locationProvider;
    [SerializeField] private bool startLocationProvider = true;

    [Header("UDP")]
    [SerializeField] private string targetAddress = "255.255.255.255";
    [SerializeField, Range(1, 65535)] private int targetPort = 47777;
    [SerializeField] private bool allowBroadcast = true;
    [SerializeField] private bool startOnEnable = true;
    [SerializeField, Min(0.05f)] private float sendIntervalSeconds = 1f;

    [Header("Android Background")]
    [SerializeField] private bool useAndroidForegroundService = true;
    [SerializeField, Min(0f)] private float foregroundServiceUpdateDistanceMeters = 1f;
    [SerializeField] private bool foregroundServiceKeepCpuAwake = true;
    [SerializeField, Min(1f)] private float androidPermissionTimeoutSeconds = 20f;

    private UdpClient _client;
    private IPEndPoint _targetEndPoint;
    private Coroutine _androidStartRoutine;
    private float _nextSendTime;
    private bool _isBroadcasting;
    private bool _usingAndroidForegroundService;

    public event Action<PhoneLocationSample> PacketSent;
    public event Action<string> Failed;

    public bool IsBroadcasting => _isBroadcasting;
    public string TargetAddress => targetAddress;
    public int TargetPort => targetPort;

    private void Awake()
    {
        if (locationProvider == null)
        {
            locationProvider = GetComponent<PhoneGpsLocationProvider>();
        }
    }

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartBroadcasting();
        }
    }

    private void OnDisable()
    {
        StopBroadcasting();
    }

    private void Update()
    {
        if (_usingAndroidForegroundService)
        {
            return;
        }

        if (!_isBroadcasting ||
            locationProvider == null ||
            !locationProvider.HasLocation ||
            Time.unscaledTime < _nextSendTime)
        {
            return;
        }

        SendNow();
        _nextSendTime = Time.unscaledTime + sendIntervalSeconds;
    }

    public void SetTarget(string address, int port)
    {
        targetAddress = address;
        targetPort = Mathf.Clamp(port, 1, 65535);

        if (_usingAndroidForegroundService)
        {
            StartAndroidForegroundService();
            return;
        }

        RebuildTargetEndPoint();
    }

    public void StartBroadcasting()
    {
        if (_isBroadcasting)
        {
            return;
        }

        if (ShouldUseAndroidForegroundService())
        {
            if (_androidStartRoutine == null)
            {
                _androidStartRoutine = StartCoroutine(StartAndroidForegroundServiceRoutine());
            }

            return;
        }

        if (locationProvider == null)
        {
            Fail("PhoneGpsLocationProvider is not assigned.");
            return;
        }

        if (startLocationProvider)
        {
            locationProvider.StartLocationService();
        }

        try
        {
            _client = new UdpClient();
            _client.EnableBroadcast = allowBroadcast;
            RebuildTargetEndPoint();
            _isBroadcasting = true;
            _nextSendTime = 0f;
        }
        catch (Exception exception)
        {
            StopBroadcasting();
            Fail("Failed to start UDP broadcaster: " + exception.Message);
        }
    }

    public void StopBroadcasting()
    {
        if (_androidStartRoutine != null)
        {
            StopCoroutine(_androidStartRoutine);
            _androidStartRoutine = null;
        }

        if (_usingAndroidForegroundService)
        {
            AndroidPhoneLocationForegroundService.Stop();
            _usingAndroidForegroundService = false;
        }

        _isBroadcasting = false;

        if (_client != null)
        {
            _client.Close();
            _client = null;
        }
    }

    public bool SendNow()
    {
        if (_usingAndroidForegroundService)
        {
            return false;
        }

        if (!_isBroadcasting || _client == null || _targetEndPoint == null)
        {
            return false;
        }

        if (locationProvider == null || !locationProvider.HasLocation)
        {
            return false;
        }

        return Send(locationProvider.LastSample);
    }

    public bool Send(PhoneLocationSample sample)
    {
        if (!sample.IsValid)
        {
            return false;
        }

        try
        {
            var packet = PhoneLocationPacket.Create(sample);
            var json = JsonUtility.ToJson(packet);
            var bytes = Encoding.UTF8.GetBytes(json);
            _client.Send(bytes, bytes.Length, _targetEndPoint);
            PacketSent?.Invoke(sample);
            return true;
        }
        catch (Exception exception)
        {
            Fail("Failed to send phone location packet: " + exception.Message);
            return false;
        }
    }

    private bool ShouldUseAndroidForegroundService()
    {
        return useAndroidForegroundService && AndroidPhoneLocationForegroundService.IsSupported;
    }

    private IEnumerator StartAndroidForegroundServiceRoutine()
    {
        AndroidPhoneLocationForegroundService.RequestMissingPermissions();

        var remaining = androidPermissionTimeoutSeconds;
        while (!AndroidPhoneLocationForegroundService.HasRequiredPermissions() && remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        _androidStartRoutine = null;

        if (!AndroidPhoneLocationForegroundService.HasRequiredPermissions())
        {
            Fail("Android foreground location service permissions were not granted.");
            yield break;
        }

        StartAndroidForegroundService();
        _usingAndroidForegroundService = true;
        _isBroadcasting = true;
    }

    private void StartAndroidForegroundService()
    {
        AndroidPhoneLocationForegroundService.Start(
            targetAddress,
            targetPort,
            sendIntervalSeconds,
            foregroundServiceUpdateDistanceMeters,
            SystemInfo.deviceUniqueIdentifier,
            allowBroadcast,
            foregroundServiceKeepCpuAwake);
    }

    private void RebuildTargetEndPoint()
    {
        IPAddress address;
        if (!IPAddress.TryParse(targetAddress, out address))
        {
            var addresses = Dns.GetHostAddresses(targetAddress);
            if (addresses == null || addresses.Length == 0)
            {
                throw new InvalidOperationException("Cannot resolve target address: " + targetAddress);
            }

            address = addresses[0];
        }

        _targetEndPoint = new IPEndPoint(address, targetPort);
    }

    private void Fail(string error)
    {
        Failed?.Invoke(error);
        Debug.LogWarning(error, this);
    }
    
}
