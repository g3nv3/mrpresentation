using UnityEngine;

public sealed class UiStateCanvasGroupRenderer : UiStateRenderer
{
    [SerializeField] private CanvasGroup target;
    [SerializeField] private bool applyAlpha = true;
    [SerializeField] private bool applyInteractable;
    [SerializeField] private bool applyBlocksRaycasts;

    private void Reset()
    {
        target = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<CanvasGroup>();
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null || style == null)
        {
            return;
        }

        if (applyAlpha)
        {
            target.alpha = style.Alpha;
        }

        if (applyInteractable)
        {
            target.interactable = style.Interactable;
        }

        if (applyBlocksRaycasts)
        {
            target.blocksRaycasts = style.Interactable;
        }
    }
}
