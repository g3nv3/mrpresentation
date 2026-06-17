using UnityEngine;

public abstract class UiStateRenderer : MonoBehaviour
{
    public abstract void Render(UiControlState state, string message, UiControlStateStyle style);
}
