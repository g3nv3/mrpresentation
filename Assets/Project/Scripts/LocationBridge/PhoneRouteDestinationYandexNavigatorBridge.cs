using System;
using System.Net;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneRouteDestinationYandexNavigatorBridge : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhoneRouteDestinationUdpReceiver receiver;
    [SerializeField] private YandexHelmetNavigator navigator;
    [SerializeField] private PhoneLocationYandexNavigatorBridge locationBridge;

    [Header("Behavior")]
    [SerializeField] private bool buildRouteOnDestinationReceived;
    [SerializeField] private bool applyLatestPhoneLocationBeforeRoute = true;

    public PhoneRouteDestination? LatestAppliedDestination { get; private set; }
    public event Action<YandexRouteResult> RouteRequestCompleted;
    public event Action RouteDisabled;

    private void Awake()
    {
        if (receiver == null)
        {
            receiver = GetComponent<PhoneRouteDestinationUdpReceiver>();
        }

        if (navigator == null)
        {
            navigator = GetComponent<YandexHelmetNavigator>();
        }

        if (locationBridge == null)
        {
            locationBridge = GetComponent<PhoneLocationYandexNavigatorBridge>();
        }
    }

    private void OnEnable()
    {
        if (receiver != null)
        {
            receiver.DestinationReceived += OnDestinationReceived;
        }

        if (navigator != null)
        {
            navigator.RouteRequestCompleted += OnRouteRequestCompleted;
            navigator.RouteDisabled += OnNavigatorRouteDisabled;
        }
    }

    private void OnDisable()
    {
        if (receiver != null)
        {
            receiver.DestinationReceived -= OnDestinationReceived;
        }

        if (navigator != null)
        {
            navigator.RouteRequestCompleted -= OnRouteRequestCompleted;
            navigator.RouteDisabled -= OnNavigatorRouteDisabled;
        }
    }

    public bool ApplyLatestDestination()
    {
        if (receiver == null || !receiver.HasDestination)
        {
            return false;
        }

        return Apply(receiver.LatestDestination);
    }

    public bool Apply(PhoneRouteDestination destination)
    {
        if (navigator == null || !destination.IsValid)
        {
            return false;
        }

        if (applyLatestPhoneLocationBeforeRoute && locationBridge != null)
        {
            locationBridge.ApplyLatestLocation();
        }

        LatestAppliedDestination = destination;
        navigator.ShowRouteTo(destination.ToGeoCoordinate());
        return true;
    }

    public void DisableRoute()
    {
        if (navigator != null)
        {
            navigator.DisableRoute();
            return;
        }

        RouteDisabled?.Invoke();
    }

    private void OnDestinationReceived(PhoneRouteDestination destination, IPEndPoint remoteEndPoint)
    {
        if (buildRouteOnDestinationReceived)
        {
            Apply(destination);
        }
    }

    private void OnRouteRequestCompleted(YandexRouteResult result)
    {
        RouteRequestCompleted?.Invoke(result);
    }

    private void OnNavigatorRouteDisabled()
    {
        RouteDisabled?.Invoke();
    }
}
