using UnityEngine;

[CreateAssetMenu(
    fileName = "LocalAvoidanceSettings",
    menuName = "Flow Field/Steering/Local Avoidance Settings")]
public class LocalAvoidanceSettings : ScriptableObject
{
    [Range(0,5)]public int AvoidanceNodeRadius = 2;
    public float ActionRadius = 2f;
    public float Strength = 10f;
}