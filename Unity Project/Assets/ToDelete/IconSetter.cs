using UnityEditor;
using UnityEngine;

public static class IconSetter
{
    [MenuItem("Tools/Set My Icon")]
    static public void SetMyIcon()
    {
        var importer = AssetImporter.GetAtPath("Assets/Scripts/Agents/Steerings/LocalAvoidanceSteering.cs") as MonoImporter;

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Gizmos/Steering Icon.png");

        importer.SetIcon(icon);
        importer.SaveAndReimport();

        AssetDatabase.Refresh();
    }
}