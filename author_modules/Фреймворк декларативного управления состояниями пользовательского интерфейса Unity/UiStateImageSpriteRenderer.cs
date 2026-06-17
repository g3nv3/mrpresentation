using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class UiStateImageSpriteRenderer : UiStateRenderer
{
    [SerializeField] private Image target;
    [SerializeField] private bool useThemeIcon = true;
    [SerializeField] private bool hideWhenNoSprite;
    [SerializeField] private UiStateSpriteEntry[] sprites;

    private Sprite _fallbackSprite;

    private void Reset()
    {
        target = GetComponent<Image>();
    }

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<Image>();
        }

        if (target != null)
        {
            _fallbackSprite = target.sprite;
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (target == null)
        {
            return;
        }

        var sprite = ResolveSprite(state, style);
        target.sprite = sprite;

        bool show = sprite != null || !hideWhenNoSprite;
        if (style != null)
        {
            show = show && style.ShowIcon;
        }

        target.gameObject.SetActive(show);
    }

    private Sprite ResolveSprite(UiControlState state, UiControlStateStyle style)
    {
        for (int i = 0; sprites != null && i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].State == state && sprites[i].Sprite != null)
            {
                return sprites[i].Sprite;
            }
        }

        if (useThemeIcon && style != null && style.IconSprite != null)
        {
            return style.IconSprite;
        }

        return _fallbackSprite;
    }
}

[Serializable]
public sealed class UiStateSpriteEntry
{
    [SerializeField] private UiControlState state;
    [SerializeField] private Sprite sprite;

    public UiControlState State => state;
    public Sprite Sprite => sprite;
}
