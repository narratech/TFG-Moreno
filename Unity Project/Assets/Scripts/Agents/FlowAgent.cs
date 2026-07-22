using System.Collections.Generic;
using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] private MonoBehaviour provider;
    [SerializeField] private float speed = 5f;

    private IAgentSteering[] _steerings;

    public INavGraph Graph { get; private set; }

    public int TargetNode { get; private set; } = -1;

    public int CurrentNode { get; private set; }

    public int CurrentRegion { get; private set; }

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

        if (TargetNode > 0)
        {
            Vector3 direction = ComputeDirection();
            Move(direction);
        }
    }

    private Vector3 ComputeDirection()
    {
        Vector3 direction = Vector3.zero;

        foreach (IAgentSteering steering in _steerings)
        {
            if (steering == null || !steering.enabled) continue;

            direction += steering.GetDirection(this) * steering.Weight;

        }
        direction.Normalize();
        return direction;
    }

    private void Move(Vector3 direction)
    {
        if (direction == Vector3.zero)
            return;

        transform.position +=
            direction * speed * Time.deltaTime;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            10f * Time.deltaTime);
    }

    public void SetDestination(int targetNode)
    {
        if (TargetNode == targetNode)
            return;

        TargetNode = targetNode;

        FlowFieldAgentManager.Instance.Subscribe(this);

    }
}