using System;
using UnityEngine;

namespace Project.Scripts.UI
{
    public interface IUiSelectedTargetState
    {
        GameObject CurrentTarget { get; }
        event Action<GameObject> Changed;
        void Select(GameObject target);
        void Clear(GameObject target);
        bool IsSelected(GameObject target);
    }
}
