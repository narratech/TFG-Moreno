using System.Collections.Generic;
using UnityEngine;

public static class FlowFieldEngine
{
    public static void CalculateFlowField(INavGraph graph, int regionId, int targetNode, FlowField data)
    {
        GenerateIntegrationField(graph, regionId, targetNode, data);
        GenerateContinuousDirections(graph, regionId, data);
    }

    private static void GenerateIntegrationField(INavGraph graph, int regionId, int targetNode, FlowField data)
    {
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        data.IntegrationField[targetNode] = 0;
        pq.Enqueue(targetNode, 0);

        while (pq.Count > 0)
        {
            int curr = pq.Dequeue();
            float currDist = data.IntegrationField[curr];

            foreach (int neighbor in graph.GetNeighbors(curr))
            {
                // Filtro por región y caminabilidad
                if (graph.GetRegionId(neighbor) != regionId || !graph.IsWalkable(neighbor))
                    continue;

                float stepDist = graph.GetDistanceBetweenNeighbors(curr, neighbor);
                float newDist = currDist + (stepDist * graph.GetNodeCost(neighbor));

                if (newDist < data.IntegrationField[neighbor])
                {
                    data.IntegrationField[neighbor] = newDist;
                    pq.Enqueue(neighbor, newDist);
                }
            }
        }
    }

    private static void GenerateContinuousDirections(INavGraph graph, int regionId, FlowField data)
    {
        for (int i = 0; i < graph.NodeCount; i++)
        {
            if (graph.GetRegionId(i) != regionId || !graph.IsWalkable(i)) continue;
            if (data.IntegrationField[i] == 0) continue; // Es el target

            Vector3 currentPos = graph.GetNodePosition(i);
            Vector3 flowDir = Vector3.zero;
            float totalWeight = 0f;

            foreach (int neighbor in graph.GetNeighbors(i))
            {
                if (graph.GetRegionId(neighbor) != regionId) continue;

                float costDiff = data.IntegrationField[i] - data.IntegrationField[neighbor];

                if (costDiff > 0 && data.IntegrationField[neighbor] != float.MaxValue)
                {
                    Vector3 dir = (graph.GetNodePosition(neighbor) - currentPos).normalized;
                    flowDir += dir * costDiff;
                    totalWeight += costDiff;
                }
            }

            if (totalWeight > 0)
                data.FlowDirections[i] = flowDir.normalized;
        }
    }
}