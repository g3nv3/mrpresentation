using System;
using UnityEngine;

public sealed class UiStateObjectVisibilityRenderer : UiStateRenderer
{
    [SerializeField] private GameObject target;
    [SerializeField] private bool defaultVisible = true;
    [SerializeField] private UiStateVisibilityEntry[] visibility;

    private void Reset()
    {
        target = gameObject;
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(ResolveVisible(state));
    }

    private bool ResolveVisible(UiControlState state)
    {
        for (int i = 0; visibility != null && i < visibility.Length; i++)
        {
            if (visibility[i] != null && visibility[i].State == state)
            {
                return visibility[i].Visible;
            }
        }

        return defaultVisible;
    }
}

[Serializable]
public sealed class UiStateVisibilityEntry
{
    [SerializeField] private UiControlState state;
    [SerializeField] private bool visible = true;

    public UiControlState State => state;
    public bool Visible => visible;
}
