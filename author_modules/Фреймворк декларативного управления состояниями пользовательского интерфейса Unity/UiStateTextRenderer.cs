using System;
using TMPro;
using UnityEngine;

public sealed class UiStateTextRenderer : UiStateRenderer
{
    [SerializeField] private TMP_Text target;
    [SerializeField] private bool useMessageWhenPresent = true;
    [SerializeField] private bool useThemeLabel = true;
    [SerializeField] private string fallbackText;
    [SerializeField] private UiStateTextEntry[] texts;

    private void Reset()
    {
        target = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<TMP_Text>();
        }

        if (string.IsNullOrEmpty(fallbackText) && target != null)
        {
            fallbackText = target.text;
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null)
        {
            return;
        }

        target.text = ResolveText(state, message, style);

        if (style != null)
        {
            target.color = style.TextColor;
        }
    }

    private string ResolveText(UiControlState state, string message, UiControlStateStyle style)
    {
        if (useMessageWhenPresent && !string.IsNullOrEmpty(message))
        {
            return message;
        }

        for (int i = 0; texts != null && i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].State == state && !string.IsNullOrEmpty(texts[i].Text))
            {
                return texts[i].Text;
            }
        }

        if (useThemeLabel && style != null && !string.IsNullOrEmpty(style.LabelOverride))
        {
            return style.LabelOverride;
        }

        return fallbackText;
    }
}

[Serializable]
public sealed class UiStateTextEntry
{
    [SerializeField] private UiControlState state;
    [SerializeField] private string text;

    public UiControlState State => state;
    public string Text => text;
}
