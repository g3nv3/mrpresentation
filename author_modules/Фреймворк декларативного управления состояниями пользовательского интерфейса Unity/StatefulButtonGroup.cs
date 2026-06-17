using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatefulButtonGroup : MonoBehaviour
{
    [SerializeField] private StatefulButtonItem[] items;
    [SerializeField] private bool allowNoSelection = true;
    [SerializeField] private string selectedId;

    public string SelectedId => selectedId;

    private void OnEnable()
    {
        Refresh();
    }

    public void Select(string id)
    {
        selectedId = id;
        Refresh();
    }

    public void ClearSelection()
    {
        if (!allowNoSelection)
        {
            return;
        }

        selectedId = null;
        Refresh();
    }

    public void Refresh()
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || items[i].Button == null)
            {
                continue;
            }

            bool isSelected = !string.IsNullOrEmpty(selectedId) && items[i].Id == selectedId;
            items[i].Button.SetState(isSelected ? UiControlState.Active : UiControlState.Idle);
        }
    }
}

[Serializable]
public sealed class StatefulButtonItem
{
    [SerializeField] private string id;
    [SerializeField] private StatefulButton button;

    public string Id => id;
    public StatefulButton Button => button;
}
