using System;

public interface IUiControlStateSource
{
    event Action<UiControlState, string> StateChanged;

    UiControlState CurrentState { get; }
    string CurrentLabel { get; }
}
