using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project/UI/UI Toggle Button Binder")]
    public sealed class UiToggleButtonBinder : MonoBehaviour
    {
        [SerializeField] private UiToggleId toggleId;
        [SerializeField] private UiPressButton pressButton;
        [SerializeField] private UiToggleStateIcon stateIcon;
        [SerializeField] private bool toggleOnPress = true;

        private IObjectResolver resolver;
        private IUiToggleState state;

        [Inject]
        public void Construct(IObjectResolver injectedResolver)
        {
            resolver = injectedResolver;

            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

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
        }

        public void Bind()
        {
            resolver ??= LifetimeScope.Find<LifetimeScope>()?.Container;

            if (resolver == null)
            {
                return;
            }

            if (!resolver.TryResolve<IUiToggleState>(out var resolvedState, toggleId))
            {
                Debug.LogError($"UI toggle state '{toggleId}' is not registered.", this);
                return;
            }

            Bind(resolvedState);
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

        private void HandlePressed()
        {
            if (toggleOnPress)
            {
                state?.Toggle();
            }
        }
    }
}
