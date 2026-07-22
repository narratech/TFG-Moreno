using UnityEngine;

public abstract class IAgentSteering : MonoBehaviour
{
    [SerializeField]
    private float _weight = 1f;

    public float Weight => _weight;

    public abstract Vector3 GetDirection(FlowFieldAgent agent);
}