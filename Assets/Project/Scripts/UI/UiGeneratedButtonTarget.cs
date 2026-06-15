using System;
using UnityEngine;

namespace Project.Scripts.UI
{
    [Serializable]
    public sealed class UiGeneratedButtonTarget
    {
        [SerializeField] private string buttonName;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private GameObject target;
        [SerializeField] private UiGeneratedButtonAction action = UiGeneratedButtonAction.BuildNavMeshPathTo;

        public string ButtonName => string.IsNullOrWhiteSpace(buttonName)
            ? target != null ? target.name : string.Empty
            : buttonName;

        public GameObject ButtonPrefab => buttonPrefab;
        public GameObject Target => target;
        public UiGeneratedButtonAction Action => action;
    }
}
