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

        controller.bugPoint =
            (Transform)EditorGUILayout.ObjectField("Bug 위치", controller.bugPoint, typeof(Transform), true);

        controller.goalPoint =
            (Transform)EditorGUILayout.ObjectField("Goal 위치", controller.goalPoint, typeof(Transform), true);

        if (EditorGUI.EndChangeCheck())
        {
            controller.ApplySettings();

            EditorUtility.SetDirty(controller);
        }
    }
}