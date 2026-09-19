using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ObjectControl))]
public class ObjectControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ObjectControl controller = (ObjectControl)target;

        EditorGUI.BeginChangeCheck();

        controller.meshEnabled =
            EditorGUILayout.Toggle("Mesh 활성화", controller.meshEnabled);

        controller.colliderEnabled =
            EditorGUILayout.Toggle("Collider 활성화", controller.colliderEnabled);

        if (EditorGUI.EndChangeCheck())
        {
            controller.ApplySettings();

            EditorUtility.SetDirty(controller);
        }
    }
}