using UnityEngine;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project/UI/UI Toggle Button Binder")]
    public sealed class UiToggleButtonBinder : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour toggleStateSource;
        [SerializeField] private UiPressButton pressButton;
        [SerializeField] private UiToggleStateIcon stateIcon;
        [SerializeField] private bool toggleOnPress = true;

        private IUiToggleState state;

        private void Reset()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (pressButton != null)
            {
                pressButton.OnPressed.AddListener(HandlePressed);
            }

            Bind();
        }

        private void OnDisable()
        {
            if (pressButton != null)
            {
                pressButton.OnPressed.RemoveListener(HandlePressed);
            }

            Unbind();
        }

        private void OnValidate()
        {
            EnsureReferences();
            ValidateToggleStateSource();
        }

        public void Bind()
        {
            Bind(ResolveToggleStateSource());
        }

        public void Bind(IUiToggleState newState)
        {
            Unbind();

            state = newState;
            if (state == null)
            {
                stateIcon?.Bind(null);
                return;
            }

            stateIcon?.Bind(state);
        }

        private void Unbind()
        {
            stateIcon?.Unbind();
            state = null;
        }

        private void EnsureReferences()
        {
            if (pressButton == null)
            {
                pressButton = GetComponentInChildren<UiPressButton>(true);
            }

            if (stateIcon == null)
            {
                stateIcon = GetComponentInChildren<UiToggleStateIcon>(true);
            }
        }

        private IUiToggleState ResolveToggleStateSource()
        {
            if (toggleStateSource == null)
            {
                Debug.LogError($"{nameof(UiToggleButtonBinder)} requires a component implementing {nameof(IUiToggleState)}.", this);
                return null;
            }

            if (toggleStateSource is IUiToggleState toggleState)
            {
                return toggleState;
            }

            Debug.LogError(
                $"{toggleStateSource.GetType().Name} does not implement {nameof(IUiToggleState)}.",
                toggleStateSource);
            return null;
        }

        private void ValidateToggleStateSource()
        {
            if (toggleStateSource == null || toggleStateSource is IUiToggleState)
            {
                return;
            }

            toggleStateSource = null;
        }

        private void HandlePressed()
        {
            if (toggleOnPress)
            {
                state?.Toggle();
            }
        }
    }
}
