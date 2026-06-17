using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class NetworkAddressDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        List<string> addresses =
            LocalNetworkAddresses.GetBroadcastAddresses();

        dropdown.ClearOptions();
        dropdown.AddOptions(addresses);

        dropdown.RefreshShownValue();
    }

    public string GetSelectedAddress()
    {
        if (dropdown.options.Count == 0)
            return null;

        return dropdown.options[dropdown.value].text;
    }
}