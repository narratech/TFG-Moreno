using UnityEngine;

public abstract class IAgentSteering : MonoBehaviour
{
    [SerializeField]
    private float _weight = 1f;

    public float Weight => _weight;

    public FlowFieldAgent Agent { get; private set; }

    public void Start()
    {
        Agent = GetComponent<FlowFieldAgent>();
        if (Agent == null)
        {
            Debug.LogError($"IAgentSteering component whitout a FlowFieldAgent component asigned");
        }
    }

    public abstract Vector3 GetDirection(FlowFieldAgent agent);
}