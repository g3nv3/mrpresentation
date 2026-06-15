using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class UiWindowManager : MonoBehaviour, IUiWindowManager
    {
        [Tooltip("Список UI-окон, которыми управляет менеджер. При открытии одного окна остальные зарегистрированные окна скрываются.")]
        [SerializeField] private List<UiWindowBinding> windows = new();
        [Tooltip("Родители, в которые генератор будет добавлять runtime-кнопки для выбранного окна.")]
        [SerializeField] private List<UiGeneratedButtonParentBinding> generatedButtonParents = new();
        [Tooltip("ID окна, которое будет открыто при старте. Оставьте пустым, если стартовое окно не нужно.")]
        [SerializeField] private string initialWindowId;

        private GameObject currentWindow;
        private string currentWindowId;

        public GameObject CurrentWindow => currentWindow;
        public string CurrentWindowId => currentWindowId;

        private void Awake()
        {
            if (!string.IsNullOrWhiteSpace(initialWindowId))
            {
                Open(initialWindowId);
            }
        }

        public void Open(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                Debug.LogError("UI window id is empty.", this);
                return;
            }

            for (var i = 0; i < windows.Count; i++)
            {
                var binding = windows[i];
                if (binding.Window == null || binding.Id != windowId)
                {
                    continue;
                }

                OpenInternal(binding.Id, binding.Window);
                return;
            }

            Debug.LogError($"UI window with id '{windowId}' is not registered.", this);
        }

        public void Open(GameObject window)
        {
            if (window == null)
            {
                Debug.LogError("UI window is null.", this);
                return;
            }

            for (var i = 0; i < windows.Count; i++)
            {
                var binding = windows[i];
                if (binding.Window != window)
                {
                    continue;
                }

                OpenInternal(binding.Id, binding.Window);
                return;
            }

            Debug.LogError($"UI window '{window.name}' is not registered.", this);
        }

        public void CloseCurrent()
        {
            if (currentWindow != null)
            {
                currentWindow.SetActive(false);
            }

            currentWindow = null;
            currentWindowId = null;
        }

        public bool IsOpen(string windowId)
        {
            return currentWindow != null && currentWindow.activeSelf && currentWindowId == windowId;
        }

        public bool TryGetGeneratedButtonParent(UiGeneratedButtonWindow window, out Transform parent)
        {
            foreach (var binding in generatedButtonParents)
            {
                if (binding.Window == window && binding.Parent != null)
                {
                    parent = binding.Parent;
                    return true;
                }
            }

            parent = null;
            Debug.LogError($"Generated UI button parent for window '{window}' is not registered.", this);
            return false;
        }

        private void OpenInternal(string windowId, GameObject window)
        {
            for (var i = 0; i < windows.Count; i++)
            {
                var registeredWindow = windows[i].Window;
                if (registeredWindow != null && registeredWindow != window)
                {
                    registeredWindow.SetActive(false);
                }
            }

            window.SetActive(true);
            currentWindow = window;
            currentWindowId = windowId;
        }

        [Serializable]
        private sealed class UiWindowBinding
        {
            [Tooltip("Уникальный ID окна. По этому значению окно открывается через Open(string windowId).")]
            [SerializeField] private string id;
            [Tooltip("GameObject окна, который будет включаться и выключаться менеджером.")]
            [SerializeField] private GameObject window;

            public string Id => id;
            public GameObject Window => window;
        }

        [Serializable]
        private sealed class UiGeneratedButtonParentBinding
        {
            [SerializeField] private UiGeneratedButtonWindow window;
            [SerializeField] private Transform parent;

            public UiGeneratedButtonWindow Window => window;
            public Transform Parent => parent;
        }
    }
}
