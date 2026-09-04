using UnityEngine;

public abstract class IAgentSteering : MonoBehaviour
{
    [SerializeField]
    private float _weight = 1f;

    public float Weight => _weight;

    public NavAgent Agent { get; private set; }

    public void Start()
    {
        Agent = GetComponent<NavAgent>();
        if (Agent == null)
        {
            Debug.LogError($"IAgentSteering component whitout a FlowFieldAgent component asigned");
        }
    }

    public abstract Vector3 GetForce();
}