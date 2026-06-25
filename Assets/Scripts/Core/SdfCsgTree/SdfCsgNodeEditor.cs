#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(SdfCsgNode))]
[CanEditMultipleObjects]
public class SdfCsgNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty isGroupNodeProp = serializedObject.FindProperty("isGroupNode");
        EditorGUILayout.PropertyField(isGroupNodeProp);

        if (isGroupNodeProp.boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groupOperation"));
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("primitiveSubscriber"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif