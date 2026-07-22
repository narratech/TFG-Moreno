using UnityEngine;

public class FlowFieldSteering : IAgentSteering
{
    [Header("FlowField")]
    [SerializeField] private float _stepSize = 0.5f;

    [Header("Formation")]
    [SerializeField] private Vector3 _desiredOffset;

    private readonly int[] _nodes = new int[8];

    private Vector3 _samplePosition;
    private Vector3 _lastAgentPosition;

    public override Vector3 GetDirection(FlowFieldAgent agent)
    {
        if ((agent.transform.position - _lastAgentPosition).sqrMagnitude >= _stepSize * _stepSize)
        {
            _lastAgentPosition = agent.transform.position;
            UpdateSamplePosition(agent);
        }
        Vector3 dir = SampleFlowField(agent, _samplePosition);
        return dir;
    }

    private void UpdateSamplePosition(FlowFieldAgent agent)
    {
        INavGraph graph = agent.Graph;

        Vector3 position = agent.transform.position;

        Vector3 forward = SampleFlowField(agent, position);

        if (forward == Vector3.zero)
        {
            _samplePosition = position;
            return;
        }

        forward.Normalize();

        Vector3 offsetDir = _desiredOffset.normalized;

        int offsetSteps = GetFreeSteps(
            graph,
            position,
            offsetDir,
            _desiredOffset.magnitude);

        int forwardSteps = GetFreeSteps(
            graph,
            position,
            forward,
            _desiredOffset.magnitude);

        int steps = Mathf.Min(offsetSteps, forwardSteps);

        _samplePosition =
            position +
            offsetDir * (steps * _stepSize);
    }

    private int GetFreeSteps(
        INavGraph graph,
        Vector3 start,
        Vector3 direction,
        float maxDistance)
    {
        int maxSteps = Mathf.CeilToInt(maxDistance / _stepSize);

        int validSteps = 0;

        for (int i = 1; i <= maxSteps; i++)
        {
            Vector3 p =
                start +
                direction * (i * _stepSize);

            int node =
                graph.GetClosestNode(p);

            if (!graph.IsWalkable(node))
                break;

            validSteps++;
        }

        return validSteps;
    }

    private Vector3 SampleFlowField(
        FlowFieldAgent agent,
        Vector3 samplePosition)
    {
        INavGraph graph = agent.Graph;

        int count =
            graph.GetInterpolationNodes(
                samplePosition,
                _nodes);

        Vector3 dir = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            int node = _nodes[i];

            int region = graph.GetRegionId(node);

            FlowField field = FlowFieldManager.Instance.GetFlowField(
                    graph,
                    region,
                    agent.TargetNode);

            if (field == null)
            {
                field = FlowFieldEngine.GenerateFlowPath(graph, agent.TargetNode, node);
            }

            if (field == null)
                continue;

            dir += field.FlowDirections[
                graph.GetLocalNode(node)];
        }

        return dir.normalized;
    }
}