using System;
using System.Linq;
using UnityEngine;

namespace Project.Scripts.UI
{
    [Serializable]
    public sealed class UiToggleRegistration
    {
        [SerializeField] private UiToggleId id;
        [SerializeField] private GameObject targetObject;

        public UiToggleId Id => id;
        public GameObject TargetObject => targetObject;

        public bool TryGetState(out IUiToggleState state)
        {
            state = null;

            if (targetObject == null)
            {
                return false;
            }

            var states = targetObject
                .GetComponents<MonoBehaviour>()
                .OfType<IUiToggleState>()
                .ToArray();

            if (states.Length != 1)
            {
                return false;
            }

            state = states[0];
            return true;
        }

        public int GetStateCount()
        {
            if (targetObject == null)
            {
                return 0;
            }

            return targetObject
                .GetComponents<MonoBehaviour>()
                .OfType<IUiToggleState>()
                .Count();
        }
    }
}
