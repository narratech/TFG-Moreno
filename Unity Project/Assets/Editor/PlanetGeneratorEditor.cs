using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlanetGenerator))]
public class PlanetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlanetGenerator generator =
            (PlanetGenerator)target;

        if (GUILayout.Button("Generate Planet"))
        {
            generator.Generate();
        }
    }
}