using System.Net;
using Project.Scripts.UI;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhoneRouteDestinationLaunchUi : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhoneRouteDestinationUdpReceiver receiver;
    [SerializeField] private PhoneRouteDestinationYandexNavigatorBridge navigatorBridge;
    [SerializeField] private UiPressButton launchButton;
    [SerializeField] private UiPressButton stopButton;
    [SerializeField] private TMP_Text addressLabel;
    [SerializeField] private TMP_Text launchButtonLabel;
    [SerializeField] private TMP_Text stateLabel;

    [Header("Text")]
    [SerializeField] private string emptyAddressText = "Route not selected";
    [SerializeField] private string fallbackAddressText = "Selected point";
    [SerializeField] private string launchText = "Start route";
    [SerializeField] private string relaunchText = "Rebuild route";
    [SerializeField] private string idleStateText = "Waiting for destination";
    [SerializeField] private string readyStateText = "Destination received";
    [SerializeField] private string requestingStateText = "Building route";
    [SerializeField] private string activeStateText = "Route active";

    [Header("Behavior")]
    [SerializeField] private bool keepLaunchButtonEnabledAfterLaunch = true;
    [SerializeField] private bool stopVisibleRouteWhenNewDestinationArrives = true;

    private PhoneRouteDestination? _pendingDestination;
    private PhoneRouteDestination? _activeDestination;
    private bool _isRouteRequesting;
    private bool _hasActiveRoute;

    private void Awake()
    {
        if (receiver == null)
        {
            receiver = GetComponent<PhoneRouteDestinationUdpReceiver>();
        }

        if (navigatorBridge == null)
        {
            navigatorBridge = GetComponent<PhoneRouteDestinationYandexNavigatorBridge>();
        }

        if (launchButton == null)
        {
            launchButton = GetComponentInChildren<UiPressButton>(true);
        }

        if (addressLabel == null)
        {
            addressLabel = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnEnable()
    {
        if (receiver != null)
        {
            receiver.DestinationReceived += OnDestinationReceived;
        }

        if (launchButton != null)
        {
            launchButton.OnPressed.AddListener(LaunchPendingRoute);
        }

        if (navigatorBridge != null)
        {
            navigatorBridge.RouteRequestCompleted += OnRouteRequestCompleted;
        }

        if (stopButton != null)
        {
            stopButton.OnPressed.AddListener(StopRoute);
        }

        RefreshView();
    }

    private void OnDisable()
    {
        if (receiver != null)
        {
            receiver.DestinationReceived -= OnDestinationReceived;
        }

        if (launchButton != null)
        {
            launchButton.OnPressed.RemoveListener(LaunchPendingRoute);
        }

        if (navigatorBridge != null)
        {
            navigatorBridge.RouteRequestCompleted -= OnRouteRequestCompleted;
        }

        if (stopButton != null)
        {
            stopButton.OnPressed.RemoveListener(StopRoute);
        }
    }

    public void LaunchPendingRoute()
    {
        if (!_pendingDestination.HasValue || navigatorBridge == null)
        {
            RefreshView();
            return;
        }

        if (navigatorBridge.Apply(_pendingDestination.Value))
        {
            _activeDestination = _pendingDestination;
            _isRouteRequesting = true;
            _hasActiveRoute = false;
            RefreshView();
        }
    }

    public void StopRoute()
    {
        if (navigatorBridge != null)
        {
            navigatorBridge.DisableRoute();
        }

        _hasActiveRoute = false;
        _isRouteRequesting = false;
        _activeDestination = null;
        RefreshView();
    }

    private void OnDestinationReceived(PhoneRouteDestination destination, IPEndPoint remoteEndPoint)
    {
        if (stopVisibleRouteWhenNewDestinationArrives && _hasActiveRoute && navigatorBridge != null)
        {
            navigatorBridge.DisableRoute();
            _hasActiveRoute = false;
            _isRouteRequesting = false;
            _activeDestination = null;
        }

        _pendingDestination = destination;
        RefreshView();
    }

    private void OnRouteRequestCompleted(YandexRouteResult result)
    {
        _isRouteRequesting = false;
        _hasActiveRoute = result != null && result.Succeeded && _activeDestination.HasValue;
        RefreshView();
    }

    private void RefreshView()
    {
        var hasDestination = _pendingDestination.HasValue && _pendingDestination.Value.IsValid;

        if (launchButton != null)
        {
            launchButton.Interactable = hasDestination &&
                                        !_isRouteRequesting &&
                                        (keepLaunchButtonEnabledAfterLaunch || !_hasActiveRoute);
        }

        if (stopButton != null)
        {
            stopButton.Interactable = _hasActiveRoute;
        }

        if (addressLabel != null)
        {
            addressLabel.text = hasDestination
                ? GetAddressText(_pendingDestination.Value)
                : emptyAddressText;
        }

        if (launchButtonLabel != null)
        {
            launchButtonLabel.text = _hasActiveRoute && IsSameDestination(_pendingDestination, _activeDestination)
                ? relaunchText
                : launchText;
        }

        if (stateLabel != null)
        {
            if (_isRouteRequesting)
            {
                stateLabel.text = requestingStateText;
            }
            else if (_hasActiveRoute)
            {
                stateLabel.text = activeStateText;
            }
            else
            {
                stateLabel.text = hasDestination ? readyStateText : idleStateText;
            }
        }
    }

    private string GetAddressText(PhoneRouteDestination destination)
    {
        if (!string.IsNullOrWhiteSpace(destination.Address))
        {
            return destination.Address;
        }

        if (!string.IsNullOrWhiteSpace(destination.Title))
        {
            return destination.Title;
        }

        return fallbackAddressText;
    }

    private static bool IsSameDestination(PhoneRouteDestination? first, PhoneRouteDestination? second)
    {
        if (!first.HasValue || !second.HasValue)
        {
            return false;
        }

        var a = first.Value;
        var b = second.Value;
        return Mathf.Abs((float)(a.Latitude - b.Latitude)) < 0.0000001f &&
               Mathf.Abs((float)(a.Longitude - b.Longitude)) < 0.0000001f;
    }
}
