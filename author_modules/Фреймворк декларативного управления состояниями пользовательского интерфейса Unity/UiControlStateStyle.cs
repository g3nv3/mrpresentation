using System;
using UnityEngine;

[Serializable]
public sealed class UiControlStateStyle
{
    [SerializeField] private UiControlState state = UiControlState.Idle;
    [SerializeField] private string labelOverride;
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Color indicatorColor = Color.clear;
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private float alpha = 1f;
    [SerializeField] private bool interactable = true;
    [SerializeField] private bool showIndicator;
    [SerializeField] private bool showIcon = true;

    public UiControlState State => state;
    public string LabelOverride => labelOverride;
    public Color BackgroundColor => backgroundColor;
    public Color TextColor => textColor;
    public Color IndicatorColor => indicatorColor;
    public Sprite IconSprite => iconSprite;
    public float Alpha => alpha;
    public bool Interactable => interactable;
    public bool ShowIndicator => showIndicator;
    public bool ShowIcon => showIcon;
}
