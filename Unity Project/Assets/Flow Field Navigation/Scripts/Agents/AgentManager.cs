using System.Collections.Generic;
using System.Diagnostics;

public class AgentManager
{
    private readonly Dictionary<INavGraph, Dictionary<int, AgentGroup>> _graphs = new();

    private readonly Dictionary<NavAgent, AgentGroup> _agentGroups = new();

    public static AgentManager Instance { get; } = new();
    private AgentManager() { }

    public void Subscribe(NavAgent agent)
    {
        Unsubscribe(agent);

        if (!_graphs.TryGetValue(agent.Graph, out var routes))
        {
            routes = new Dictionary<int, AgentGroup>();
            _graphs.Add(agent.Graph, routes);
        }

        if (!routes.TryGetValue(agent.TargetNode, out AgentGroup group))
        {
            group = new AgentGroup(
                agent.Graph,
                agent.TargetNode);

            routes.Add(agent.TargetNode, group);

            FlowFieldManager.Instance.GetRoute(
                agent.Graph,
                agent.TargetNode);
        }

        group.Agents.Add(agent);
        _agentGroups.Add(agent, group);
    }

    public void Unsubscribe(NavAgent agent)
    {
        if (!_agentGroups.TryGetValue(agent, out AgentGroup group))
            return;

        group.Agents.Remove(agent);
        _agentGroups.Remove(agent);

        if (group.Agents.Count == 0)
        {
            FlowFieldManager.Instance.EliminateRoute(
                group.Graph,
                group.TargetNode);

            _graphs[group.Graph].Remove(group.TargetNode);

            if (_graphs[group.Graph].Count == 0)
                _graphs.Remove(group.Graph);
        }
    }

    public FlowField GetFlowField(
        NavAgent agent,
        int region)
    {
        if (!_agentGroups.TryGetValue(agent, out AgentGroup group))
            return null;

        return FlowFieldManager.Instance.GetFlowField(
            group.Graph,
            region,
            group.TargetNode);
    }

    public IReadOnlyList<NavAgent> GetAgents(NavAgent agent)
    {
        if (_agentGroups.TryGetValue(agent, out AgentGroup group))
            return group.Agents;

        return null;
    }
}