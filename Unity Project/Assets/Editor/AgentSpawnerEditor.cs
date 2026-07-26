#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AgentSpawner))]
public class AgentSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AgentSpawner spawner = (AgentSpawner)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Spawn"))
            spawner.Spawn();

        if (GUILayout.Button("Clear"))
            spawner.Clear();
    }
}
#endif