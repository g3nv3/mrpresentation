using UnityEngine;
using UnityEngine.UI;

public enum UiStateColorRole
{
    Background,
    Text,
    Indicator
}

public sealed class UiStateGraphicColorRenderer : UiStateRenderer
{
    [SerializeField] private Graphic target;
    [SerializeField] private UiStateColorRole colorRole = UiStateColorRole.Background;
    [SerializeField] private bool keepColorWithoutTheme = true;
    [SerializeField] private Color fallbackColor = Color.white;

    private void Reset()
    {
        target = GetComponent<Graphic>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<Graphic>();
        }

        if (target != null)
        {
            fallbackColor = target.color;
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null)
        {
            return;
        }

        if (style == null)
        {
            if (!keepColorWithoutTheme)
            {
                target.color = fallbackColor;
            }

            return;
        }

        switch (colorRole)
        {
            case UiStateColorRole.Text:
                target.color = style.TextColor;
                break;
            case UiStateColorRole.Indicator:
                target.color = style.IndicatorColor;
                break;
            default:
                target.color = style.BackgroundColor;
                break;
        }
    }
}
