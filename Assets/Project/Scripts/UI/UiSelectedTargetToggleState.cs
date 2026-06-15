using System;
using UnityEngine;

namespace Project.Scripts.UI
{
    public sealed class UiSelectedTargetToggleState : IUiToggleState
    {
        private readonly IUiSelectedTargetState selectedTargetState;
        private readonly GameObject target;
        private Action<bool> changed;

        public UiSelectedTargetToggleState(IUiSelectedTargetState selectedTargetState, GameObject target)
        {
            this.selectedTargetState = selectedTargetState;
            this.target = target;
        }

        public bool IsOn => selectedTargetState != null && selectedTargetState.IsSelected(target);

        public event Action<bool> Changed
        {
            add
            {
                if (changed == null && selectedTargetState != null)
                {
                    selectedTargetState.Changed += HandleSelectedTargetChanged;
                }

                changed += value;
            }
            remove
            {
                changed -= value;

                if (changed == null && selectedTargetState != null)
                {
                    selectedTargetState.Changed -= HandleSelectedTargetChanged;
                }
            }
        }

        public void SetOn(bool value)
        {
            if (selectedTargetState == null)
            {
                return;
            }

            if (value)
            {
                selectedTargetState.Select(target);
            }
            else
            {
                selectedTargetState.Clear(target);
            }
        }

        public void Toggle()
        {
            SetOn(!IsOn);
        }

        private void HandleSelectedTargetChanged(GameObject _)
        {
            changed?.Invoke(IsOn);
        }
    }
}
