using UnityEngine;

public class LocalAvoidanceSteering : IAgentSteering
{
    [SerializeField]
    private int avoidanceNodeRadius = 2;

    [SerializeField]
    private float actionRadius = 2f;

    [SerializeField]
    private float strength = 1f;

    private readonly int[] _nodes = new int[64];

    public void Start()
    {
        base.Start();
        LocalAvoidanceManager.Instance.Subscribe(Agent);
    }

    public override Vector3 GetDirection(FlowFieldAgent agent)
    {
        NodeAgentData[] nodeData =
            LocalAvoidanceManager.Instance.GetNodeData(agent.Graph);

        int count = agent.Graph.GetNodesInRadius(
            agent.CurrentNode,
            avoidanceNodeRadius,
            _nodes);

        Vector3 avoidance = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            NodeAgentData data = nodeData[_nodes[i]];

            if (data.Count == 0)
                continue;

            Vector3 meanPosition = (Vector3)(data.SumPosition / data.Count);

            Vector3 delta =
                agent.transform.position - meanPosition;

            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance < 0.0001f)
                continue;

            if (sqrDistance > actionRadius * actionRadius)
                continue;

            avoidance += delta.normalized * (data.Count / sqrDistance);
        }

        return avoidance.normalized * strength;
    }
}