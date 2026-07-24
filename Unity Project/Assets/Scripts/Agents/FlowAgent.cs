using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] private MonoBehaviour provider;
    [SerializeField] public float MaxSpeed = 5f;
    [SerializeField] public float MaxForce = 20f;
    [Range(0,1)][SerializeField] private float FrictionForce = 0.1f;

    private IAgentSteering[] _steerings;

    public INavGraph Graph { get; private set; }

    public int TargetNode { get; private set; } = -1;

    public int CurrentNode { get; private set; }

    public int CurrentRegion { get; private set; }

    public Vector3 Velocity { get; private set; } = Vector3.zero;


    [SerializeField] private float MinForce = 0.1f;
    [SerializeField] private float MinSpeed = 0.1f;

    private void Awake()
    {
        switch (provider)
        {
            case Grid2DProvider g:
                Graph = g.Graph;
                break;

            case Grid3DProvider g:
                Graph = g.Graph;
                break;

            case QuadSphereProvider g:
                Graph = g.Graph;
                break;
        }

        _steerings = GetComponents<IAgentSteering>();
    }

    private void Start()
    {
        if (Graph == null) {
            switch (provider)
            {
                case Grid2DProvider g:
                    Graph = g.Graph;
                    break;

                case Grid3DProvider g:
                    Graph = g.Graph;
                    break;

                case QuadSphereProvider g:
                    Graph = g.Graph;
                    break;
            }
        }
        FlowFieldAgentManager.Instance.Subscribe(this);
    }

    private void OnDestroy()
    {
        if (FlowFieldAgentManager.Instance != null)
            FlowFieldAgentManager.Instance.Unsubscribe(this);
    }

    private void Update()
    {
        if (Graph == null)
            return;

        CurrentNode = Graph.GetClosestNode(transform.position);
        CurrentRegion = Graph.GetRegionId(CurrentNode);

        Vector3 steering = ComputeSteering();

        if (steering.magnitude < MinForce)
        {
            steering = Vector3.zero;
        }

        Vector3 acceleration = steering - Velocity * FrictionForce;

        Velocity += acceleration * Time.deltaTime;

        Velocity = Vector3.ClampMagnitude(
            Velocity,
            MaxSpeed);

        if (Velocity.magnitude < MinSpeed)
            Velocity = Vector3.zero;

        transform.position +=
            Velocity * Time.deltaTime;

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = Velocity;

        Graph.ConstrainPositionAndRotation(
            ref position,
            ref velocity,
            ref rotation);

        transform.position = position;
        transform.rotation = rotation;
        Velocity = velocity;
    }

    private Vector3 ComputeSteering()
    {
        Vector3 force = Vector3.zero;

        foreach (IAgentSteering steering in _steerings)
        {
            if (steering == null || !steering.enabled)
                continue;

            Vector3 dir = steering.GetDirection(this);
            if (dir.magnitude > MinForce)
            {
                force += dir * steering.Weight;
            }
        }

        return Vector3.ClampMagnitude(force, MaxForce);
    }

    public void SetDestination(int targetNode)
    {
        if (TargetNode == targetNode)
            return;

        TargetNode = targetNode;

        FlowFieldAgentManager.Instance.Subscribe(this);

    }
}