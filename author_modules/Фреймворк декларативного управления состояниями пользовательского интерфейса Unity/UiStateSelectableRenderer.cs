using UnityEngine;
using UnityEngine.UI;

public sealed class UiStateSelectableRenderer : UiStateRenderer
{
    [SerializeField] private Selectable target;

    private void Reset()
    {
        target = GetComponent<Selectable>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<Selectable>();
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null)
        {
            return;
        }

        target.interactable = style == null
            ? state != UiControlState.Disabled && state != UiControlState.Busy
            : style.Interactable;
    }
}
