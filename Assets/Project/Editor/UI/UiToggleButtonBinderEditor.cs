using System.Collections.Generic;
using Project.Scripts.UI;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.UI
{
    [CustomEditor(typeof(UiToggleButtonBinder))]
    public sealed class UiToggleButtonBinderEditor : UnityEditor.Editor
    {
        private SerializedProperty toggleStateSource;
        private SerializedProperty pressButton;
        private SerializedProperty stateIcon;
        private SerializedProperty toggleOnPress;

        private void OnEnable()
        {
            toggleStateSource = serializedObject.FindProperty("toggleStateSource");
            pressButton = serializedObject.FindProperty("pressButton");
            stateIcon = serializedObject.FindProperty("stateIcon");
            toggleOnPress = serializedObject.FindProperty("toggleOnPress");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawToggleStateSource();
            EditorGUILayout.PropertyField(pressButton);
            EditorGUILayout.PropertyField(stateIcon);
            EditorGUILayout.PropertyField(toggleOnPress);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawToggleStateSource()
        {
            var currentSource = toggleStateSource.objectReferenceValue as MonoBehaviour;
            var currentObject = currentSource != null ? currentSource.gameObject : null;

            EditorGUI.BeginChangeCheck();
            var selectedObject = (GameObject)EditorGUILayout.ObjectField(
                "Toggle State Object",
                currentObject,
                typeof(GameObject),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                toggleStateSource.objectReferenceValue = GetFirstToggleStateComponent(selectedObject);
                currentSource = toggleStateSource.objectReferenceValue as MonoBehaviour;
                currentObject = selectedObject;
            }

            if (currentObject == null)
            {
                toggleStateSource.objectReferenceValue = null;
                EditorGUILayout.HelpBox(
                    $"Assign a GameObject with a component implementing {nameof(IUiToggleState)}.",
                    MessageType.Info);
                return;
            }

            var components = GetToggleStateComponents(currentObject);
            if (components.Count == 0)
            {
                toggleStateSource.objectReferenceValue = null;
                EditorGUILayout.HelpBox(
                    $"{currentObject.name} has no components implementing {nameof(IUiToggleState)}.",
                    MessageType.Error);
                return;
            }

            var selectedIndex = Mathf.Max(0, components.IndexOf(currentSource));
            var names = GetComponentNames(components);
            var nextIndex = EditorGUILayout.Popup("Toggle State Component", selectedIndex, names);
            toggleStateSource.objectReferenceValue = components[nextIndex];
        }

        private static MonoBehaviour GetFirstToggleStateComponent(GameObject sourceObject)
        {
            if (sourceObject == null)
            {
                return null;
            }

            var components = sourceObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IUiToggleState)
                {
                    return components[i];
                }
            }

            return null;
        }

        private static List<MonoBehaviour> GetToggleStateComponents(GameObject sourceObject)
        {
            var result = new List<MonoBehaviour>();
            if (sourceObject == null)
            {
                return result;
            }

            var components = sourceObject.GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IUiToggleState)
                {
                    result.Add(components[i]);
                }
            }

            return result;
        }

        private static string[] GetComponentNames(IReadOnlyList<MonoBehaviour> components)
        {
            var names = new string[components.Count];
            for (var i = 0; i < components.Count; i++)
            {
                names[i] = components[i].GetType().Name;
            }

            return names;
        }
    }
}
