using UnityEngine;

namespace Project.Scripts.UI
{
    public sealed class NullUiGeneratedButtonSpawner : IUiGeneratedButtonSpawner
    {
        public void Rebuild(UiGeneratedButtonSource source)
        {
            Debug.LogWarning(
                $"{nameof(UiGeneratedButtonSource)} cannot create buttons because {nameof(IUiWindowManager)} is not registered.",
                source);
        }

        public void Clear(UiGeneratedButtonSource source)
        {
        }
    }
}
