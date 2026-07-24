using Unity.Mathematics;
using UnityEngine;

public class LocalAvoidanceSteering : IAgentSteering
{
    [Header("Settings")]
    [SerializeField]
    private LocalAvoidanceSettings settings;

    [Header("Manual Settings")]
    [SerializeField]
    private int avoidanceNodeRadius = 2;

    [SerializeField]
    private float actionRadius = 2f;

    [SerializeField]
    private float strength = 10f;

    private readonly int[] _nodes = new int[64];

    private int AvoidanceNodeRadius =>
        settings != null
            ? settings.AvoidanceNodeRadius
            : avoidanceNodeRadius;

    private float ActionRadius =>
        settings != null
            ? settings.ActionRadius
            : actionRadius;

    private float Strength =>
        settings != null
            ? settings.Strength
            : strength;

    public new void Start()
    {
        Debug.Log(Strength);
        base.Start();
        LocalAvoidanceManager.Instance.Subscribe(Agent);
    }

    public override Vector3 GetDirection(FlowFieldAgent agent)
    {
        NodeAgentData[] nodeData = LocalAvoidanceManager.Instance.GetNodeData(agent.Graph);

        int count = agent.Graph.GetNodesInRadius(
            agent.CurrentNode,
            AvoidanceNodeRadius,
            _nodes);

        Vector3 force = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            NodeAgentData data = nodeData[_nodes[i]];

            if (data.Count == 0)
                continue;

            float3 sumPos = data.SumPosition;
            float3 sumVel = data.SumVelocity;
            int agentCount = data.Count;

            // Quitarnos de la media del nodo en el que estamos
            if (_nodes[i] == agent.CurrentNode)
            {
                if (agentCount == 1)
                    continue;

                sumPos -= (float3)agent.transform.position;
                sumVel -= (float3)agent.Velocity;
                agentCount--;
            }

            Vector3 meanPosition = (Vector3)(sumPos / agentCount);
            Vector3 meanVelocity = (Vector3)(sumVel / agentCount);

            Vector3 delta =
                agent.transform.position - meanPosition;

            float distance = delta.magnitude;

            if (distance < 0.001f || distance > ActionRadius)
                continue;

            float t = 1f - distance / ActionRadius;
            float weight = t * t;

            Vector3 separation = delta.normalized * weight * agentCount;

            Vector3 velocityAvoidance = agent.Velocity - meanVelocity;

            force += separation;
        }

        return force * Strength;
    }
}