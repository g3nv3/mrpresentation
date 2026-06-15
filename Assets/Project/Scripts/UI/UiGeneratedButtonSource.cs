using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class UiGeneratedButtonSource : MonoBehaviour
    {
        [SerializeField] private UiGeneratedButtonWindow window = UiGeneratedButtonWindow.NavMesh;
        [SerializeField] private bool rebuildOnStart = true;
        [SerializeField] private bool clearOnDisable = true;
        [SerializeField] private List<UiGeneratedButtonTarget> buttons = new();

        private IUiGeneratedButtonSpawner buttonSpawner;
        private bool started;

        public UiGeneratedButtonWindow Window => window;
        public IReadOnlyList<UiGeneratedButtonTarget> Buttons => buttons;

        [Inject]
        public void Construct(IUiGeneratedButtonSpawner injectedButtonSpawner)
        {
            buttonSpawner = injectedButtonSpawner;
        }

        private void Start()
        {
            started = true;

            if (rebuildOnStart)
            {
                RebuildButtons();
            }
        }

        private void OnEnable()
        {
            if (!started || !rebuildOnStart)
            {
                return;
            }

            RebuildButtons();
        }

        private void OnDisable()
        {
            if (clearOnDisable)
            {
                buttonSpawner?.Clear(this);
            }
        }

        public void RebuildButtons()
        {
            if (buttonSpawner == null)
            {
                Debug.LogWarning(
                    $"{nameof(UiGeneratedButtonSource)} is not injected. Spawn this prefab through VContainer or add it to the LifetimeScope auto-inject list.",
                    this);
                return;
            }

            buttonSpawner.Rebuild(this);
        }

        public void ClearButtons()
        {
            buttonSpawner?.Clear(this);
        }
    }
}
