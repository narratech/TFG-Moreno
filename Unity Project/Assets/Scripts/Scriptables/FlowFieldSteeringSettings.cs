using UnityEngine;

[CreateAssetMenu(
    fileName = "FlowFieldSteeringSettings",
    menuName = "Flow Field/Steering/Flow Field Settings")]
public class FlowFieldSteeringSettings : ScriptableObject
{
    public float StepSize = 0.5f;
    public float TimeStamp = 0.2f;
}