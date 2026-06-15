using System;

namespace Project.Scripts.UI
{
    public interface IUiToggleState
    {
        bool IsOn { get; }
        event Action<bool> Changed;
        void SetOn(bool value);
        void Toggle();
    }
}
