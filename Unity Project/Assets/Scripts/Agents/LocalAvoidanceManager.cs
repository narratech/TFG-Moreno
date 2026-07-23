using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct NodeAgentData
{
    public ushort Count;

    public float3 SumPosition;

    public float3 SumVelocity;
}

public class LocalAvoidanceManager : MonoBehaviour
{
    public static LocalAvoidanceManager Instance { get; private set; }

    private readonly List<FlowFieldAgent> _agents = new();

    private readonly Dictionary<INavGraph, NodeAgentData[]> _graphs = new();

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterGraph(INavGraph graph)
    {
        if (_graphs.ContainsKey(graph))
            return;

        _graphs.Add( graph, new NodeAgentData[graph.NodeCount]);
    }

    public void Subscribe(FlowFieldAgent agent)
    {
        RegisterGraph(agent.Graph);

        if (!_agents.Contains(agent))
            _agents.Add(agent);
    }

    public void Unsubscribe(FlowFieldAgent agent)
    {
        _agents.Remove(agent);
    }

    private void LateUpdate()
    {
        foreach (NodeAgentData[] data in _graphs.Values)
            Array.Clear(data, 0, data.Length);

        foreach (FlowFieldAgent agent in _agents)
        {
            if (agent.Graph == null)
                continue;

            ref NodeAgentData node = ref _graphs[agent.Graph][agent.CurrentNode];

            node.Count++;
            node.SumPosition += (float3)agent.transform.position;
            node.SumVelocity += (float3)agent.Velocity;
        }
    }

    public NodeAgentData[] GetNodeData(INavGraph graph)
    {
        return _graphs[graph];
    }
}