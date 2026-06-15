using UnityEngine;

namespace Project.Scripts.UI
{
    public sealed class UiGeneratedButtonOptions
    {
        public GameObject ButtonPrefab { get; }
        public NavMeshPathPresenter NavMeshPathPresenter { get; }

        public UiGeneratedButtonOptions(
            GameObject buttonPrefab,
            NavMeshPathPresenter navMeshPathPresenter)
        {
            ButtonPrefab = buttonPrefab;
            NavMeshPathPresenter = navMeshPathPresenter;
        }
    }
}