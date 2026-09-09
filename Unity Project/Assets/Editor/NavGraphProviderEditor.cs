#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavGraphProvider), true)]
[CanEditMultipleObjects]
public class NavGraphProviderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        List<SerializedProperty> baseProperties = new List<SerializedProperty>();
        List<SerializedProperty> derivedProperties = new List<SerializedProperty>();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script") continue;

            // Identifica si el campo pertenece a NavGraphProvider o a la clase hija
            FieldInfo fieldInfo = target.GetType().GetField(
                iterator.name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            if (fieldInfo != null && fieldInfo.DeclaringType == typeof(NavGraphProvider))
            {
                baseProperties.Add(iterator.Copy());
            }
            else
            {
                derivedProperties.Add(iterator.Copy());
            }
        }

        // 1. Dibujar primero la configuración específica del hijo (Voxel, Grid, Geodesic)
        foreach (var prop in derivedProperties)
        {
            EditorGUILayout.PropertyField(prop, true);
        }

        EditorGUILayout.Space(10);

        // 2. Dibujar al final la configuración base de NavGraphProvider
        foreach (var prop in baseProperties)
        {
            EditorGUILayout.PropertyField(prop, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif