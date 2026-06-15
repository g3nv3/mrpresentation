using UnityEngine;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project/UI/UI Toggle State Icon")]
    public class UiToggleStateIcon : MonoBehaviour
    {
        [SerializeField] private GameObject targetObject;

        private IUiToggleState state;

        private void Reset()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Bind(IUiToggleState newState)
        {
            if (ReferenceEquals(state, newState))
            {
                Refresh(state?.IsOn ?? false);
                return;
            }

            Unbind();
            state = newState;

            if (state == null)
            {
                Refresh(false);
                return;
            }

            state.Changed += Refresh;
            Refresh(state.IsOn);
        }

        public void Unbind()
        {
            if (state != null)
            {
                state.Changed -= Refresh;
                state = null;
            }
        }

        private void Refresh(bool isOn)
        {
            EnsureReferences();

            if (targetObject == null)
            {
                return;
            }

            targetObject.SetActive(isOn);
        }

        private void EnsureReferences()
        {
            if (targetObject == null)
            {
                targetObject = gameObject;
            }
        }
    }
}
