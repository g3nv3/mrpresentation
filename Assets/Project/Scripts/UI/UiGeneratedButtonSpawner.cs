using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Project.Scripts.UI
{
    public sealed class UiGeneratedButtonSpawner : IUiGeneratedButtonSpawner
    {
        private readonly IObjectResolver objectResolver;
        private readonly IUiWindowManager windowManager;
        private readonly IUiSelectedTargetState selectedTargetState;
        private readonly UiGeneratedButtonOptions options;
        private readonly Dictionary<UiGeneratedButtonSource, List<GameObject>> buttonsBySource = new();

        public UiGeneratedButtonSpawner(
            IObjectResolver objectResolver,
            IUiWindowManager windowManager,
            IUiSelectedTargetState selectedTargetState,
            UiGeneratedButtonOptions options)
        {
            this.objectResolver = objectResolver;
            this.windowManager = windowManager;
            this.selectedTargetState = selectedTargetState;
            this.options = options;
        }

        public void Rebuild(UiGeneratedButtonSource source)
        {
            if (source == null)
            {
                return;
            }

            Clear(source);

            if (!windowManager.TryGetGeneratedButtonParent(source.Window, out var parent))
            {
                return;
            }

            var createdButtons = new List<GameObject>();
            var definitions = source.Buttons;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || definition.Target == null)
                {
                    continue;
                }

                if (definition.ButtonPrefab == null)
                {
                    Debug.LogError($"Generated UI button prefab is not assigned for '{definition.ButtonName}'.", source);
                    continue;
                }

                var button = objectResolver.Instantiate(definition.ButtonPrefab, parent, false);
                ConfigureButton(button, definition);
                createdButtons.Add(button);
            }

            if (createdButtons.Count > 0)
            {
                buttonsBySource[source] = createdButtons;
            }
        }

        public void Clear(UiGeneratedButtonSource source)
        {
            if (source == null || !buttonsBySource.TryGetValue(source, out var buttons))
            {
                return;
            }

            for (var i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null)
                {
                    Object.Destroy(buttons[i]);
                }
            }

            buttonsBySource.Remove(source);
        }

        private void ConfigureButton(GameObject buttonObject, UiGeneratedButtonTarget definition)
        {
            var labelText = definition.ButtonName;
            buttonObject.name = string.IsNullOrWhiteSpace(labelText)
                ? "Generated UI Button"
                : $"Generated UI Button - {labelText}";

            if (buttonObject.TryGetComponent<UiGeneratedButtonView>(out var view))
            {
                view.SetLabel(labelText);
                BindSelectionIcon(view.StateIcon, definition.Target);
                BindPress(view.PressButton, buttonObject, definition);
                return;
            }

            Debug.LogError("Generated UI button prefab has no UiGeneratedButtonView component.", buttonObject);
        }

        private void BindSelectionIcon(UiToggleStateIcon stateIcon, GameObject target)
        {
            if (stateIcon == null || target == null)
            {
                return;
            }

            stateIcon.Bind(new UiSelectedTargetToggleState(selectedTargetState, target));
        }

        private void BindPress(UiPressButton button, Object context, UiGeneratedButtonTarget definition)
        {
            if (button == null)
            {
                Debug.LogError("Generated UI button prefab has no UiPressButton component.", context);
                return;
            }

            button.OnPressed.AddListener(() => Execute(definition));
        }

        private void Execute(UiGeneratedButtonTarget definition)
        {
            switch (definition.Action)
            {
                case UiGeneratedButtonAction.BuildNavMeshPathTo:
                    BuildNavMeshPathTo(definition.Target);
                    break;
                default:
                    Debug.LogError($"Generated UI button action '{definition.Action}' is not supported.");
                    break;
            }
        }

        private void BuildNavMeshPathTo(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (options.NavMeshPathPresenter == null)
            {
                Debug.LogError("NavMeshPathPresenter is not assigned for generated UI buttons.");
                return;
            }

            options.NavMeshPathPresenter.BuildPathTo(target.transform);

            if (options.NavMeshPathPresenter.IsActive &&
                options.NavMeshPathPresenter.CurrentTarget == target.transform)
            {
                selectedTargetState?.Select(target);
                return;
            }

            selectedTargetState?.Clear(target);
        }
    }
}
