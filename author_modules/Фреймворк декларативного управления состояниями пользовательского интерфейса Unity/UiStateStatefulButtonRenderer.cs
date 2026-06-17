using UnityEngine;

public sealed class UiStateStatefulButtonRenderer : UiStateRenderer
{
    [SerializeField] private StatefulButton target;

    private void Reset()
    {
        target = GetComponent<StatefulButton>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<StatefulButton>();
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            target.SetState(state);
        }
        else
        {
            target.SetState(state, message);
        }
    }
}
