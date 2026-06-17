using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UiControlStateEmitter : MonoBehaviour, IUiControlStateSource
{
    [SerializeField] private UiControlState initialState = UiControlState.Idle;
    [SerializeField] private string initialLabel;

    public event Action<UiControlState, string> StateChanged;

    public UiControlState CurrentState { get; private set; }
    public string CurrentLabel { get; private set; }

    private void Awake()
    {
        CurrentState = initialState;
        CurrentLabel = initialLabel;
    }

    private void OnEnable()
    {
        Emit();
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
        SetState(state, CurrentLabel);
    }

    public void SetState(UiControlState state, string label)
    {
        if (CurrentState == state && CurrentLabel == label)
        {
            return;
        }

        CurrentState = state;
        CurrentLabel = label;
        Emit();
    }

    public void SetLabel(string label)
    {
        SetState(CurrentState, label);
    }

    private void Emit()
    {
        StateChanged?.Invoke(CurrentState, CurrentLabel);
    }
}
