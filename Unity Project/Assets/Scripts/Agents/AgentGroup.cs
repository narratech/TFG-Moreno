using System.Collections.Generic;
public class AgentGroup
{
    public INavGraph Graph { get; }

    public int TargetNode { get; }

    public List<FlowFieldAgent> Agents { get; }
        = new();

    public AgentGroup(
        INavGraph graph,
        int targetNode)
    {
        Graph = graph;
        TargetNode = targetNode;
    }
}
