using System;
using UnityEngine;

namespace Project.Scripts.UI
{
    public sealed class UiSelectedTargetState : IUiSelectedTargetState
    {
        public GameObject CurrentTarget { get; private set; }
        public event Action<GameObject> Changed;

        public void Select(GameObject target)
        {
            if (CurrentTarget == target)
            {
                return;
            }

            CurrentTarget = target;
            Changed?.Invoke(CurrentTarget);
        }

        public void Clear(GameObject target)
        {
            if (CurrentTarget != target)
            {
                return;
            }

            CurrentTarget = null;
            Changed?.Invoke(CurrentTarget);
        }

        public bool IsSelected(GameObject target)
        {
            return CurrentTarget == target;
        }
    }
}
