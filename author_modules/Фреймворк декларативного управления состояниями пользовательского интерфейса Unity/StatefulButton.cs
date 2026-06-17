using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StatefulButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Graphic background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Graphic indicator;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("State")]
    [SerializeField] private UiControlStateTheme theme;
    [SerializeField] private UiControlState initialState = UiControlState.Idle;
    [SerializeField] private string defaultLabel;
    [SerializeField] private bool updateButtonInteractable = true;

    public UiControlState CurrentState { get; private set; }
    public Button Button => button;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        if (string.IsNullOrEmpty(defaultLabel) && label != null)
        {
            defaultLabel = label.text;
        }
    }

    private void OnEnable()
    {
        SetState(initialState);
    }

    public void SetTheme(UiControlStateTheme value)
    {
        theme = value;
        ApplyState(CurrentState, null);
    }

    public void SetIdle()
    {
        SetState(UiControlState.Idle);
    }

    public void SetActive()
    {
        SetState(UiControlState.Active);
    }

    public void SetBusy()
    {
        SetState(UiControlState.Busy);
    }

    public void SetSuccess()
    {
        SetState(UiControlState.Success);
    }

    public void SetWarning()
    {
        SetState(UiControlState.Warning);
    }

    public void SetError()
    {
        SetState(UiControlState.Error);
    }

    public void SetDisabled()
    {
        SetState(UiControlState.Disabled);
    }

    public void SetState(UiControlState state)
    {
        ApplyState(state, null);
    }

    public void SetState(UiControlState state, string labelText)
    {
        ApplyState(state, labelText);
    }

    public void SetLabel(string labelText)
    {
        if (label != null)
        {
            label.text = labelText;
        }
    }

    private void ApplyState(UiControlState state, string labelText)
    {
        CurrentState = state;

        var style = theme != null ? theme.GetStyle(state) : null;
        if (style == null)
        {
            ApplyFallbackState(state, labelText);
            return;
        }

        if (background != null)
        {
            background.color = style.BackgroundColor;
        }

        if (label != null)
        {
            label.color = style.TextColor;
            label.text = ResolveLabel(style, labelText);
        }

        if (indicator != null)
        {
            indicator.gameObject.SetActive(style.ShowIndicator);
            indicator.color = style.IndicatorColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = style.Alpha;
        }

        if (button != null && updateButtonInteractable)
        {
            button.interactable = style.Interactable;
        }
    }

    private void ApplyFallbackState(UiControlState state, string labelText)
    {
        if (label != null)
        {
            label.text = string.IsNullOrEmpty(labelText) ? defaultLabel : labelText;
        }

        if (indicator != null)
        {
            indicator.gameObject.SetActive(state == UiControlState.Active ||
                                           state == UiControlState.Busy ||
                                           state == UiControlState.Error);
        }

        if (button != null && updateButtonInteractable)
        {
            button.interactable = state != UiControlState.Disabled && state != UiControlState.Busy;
        }
    }

    private string ResolveLabel(UiControlStateStyle style, string labelText)
    {
        if (!string.IsNullOrEmpty(labelText))
        {
            return labelText;
        }

        if (!string.IsNullOrEmpty(style.LabelOverride))
        {
            return style.LabelOverride;
        }

        return defaultLabel;
    }

    private void ResolveReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (background == null && button != null)
        {
            background = button.targetGraphic;
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
