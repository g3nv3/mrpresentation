using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(QrMarkerRegistry))]
public sealed class QrMarkerRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Open QR Generator"))
        {
            QrMarkerGeneratorWindow.OpenWindow((QrMarkerRegistry)target);
        }
    }
}
