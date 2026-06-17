using UnityEngine;

[CreateAssetMenu(menuName = "UI/Control State Theme", fileName = "UiControlStateTheme")]
public sealed class UiControlStateTheme : ScriptableObject
{
    [SerializeField] private UiControlStateStyle fallbackStyle;
    [SerializeField] private UiControlStateStyle[] styles;

    public UiControlStateStyle GetStyle(UiControlState state)
    {
        if (styles != null)
        {
            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] != null && styles[i].State == state)
                {
                    return styles[i];
                }
            }
        }

        return fallbackStyle;
    }
}
