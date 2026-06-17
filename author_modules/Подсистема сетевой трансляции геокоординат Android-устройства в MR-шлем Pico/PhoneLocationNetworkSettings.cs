using TMPro;
using UnityEngine;

public sealed class PhoneLocationNetworkSettings : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown addressDropdown;
    [SerializeField] private PhoneLocationUdpBroadcaster broadcaster;
    [SerializeField] private PhoneRouteDestinationUdpBroadcaster routeDestinationBroadcaster;

    public void ApplySelectedAddress()
    {
        if (addressDropdown.options.Count == 0)
            return;

        string address =
            addressDropdown.options[addressDropdown.value].text;

        if (broadcaster != null)
        {
            broadcaster.SetTarget(
                address,
                broadcaster.TargetPort
            );
        }

        if (routeDestinationBroadcaster != null)
        {
            routeDestinationBroadcaster.SetTarget(
                address,
                routeDestinationBroadcaster.TargetPort
            );
        }
    }
}
