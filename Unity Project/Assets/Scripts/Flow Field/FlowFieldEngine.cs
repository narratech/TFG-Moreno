using System.Collections.Generic;
using UnityEngine;

public static class FlowFieldEngine
{
    public static FlowField CalculateFlowField(INavGraph graph, int regionId, int targetNode)
    {
        FlowField data = new FlowField(graph.GetRegionSize(regionId), targetNode);
        GenerateIntegrationField(graph, regionId, targetNode, data);
        GenerateVectorField(graph, regionId, data);
        return data;
    }

    private static void GenerateIntegrationField(INavGraph graph, int regionId, int globalTargetNode, FlowField data)
    {
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        // Usamos el mapeo para escribir en el array pequeño del FlowField
        int localTarget = graph.GetLocalNode(globalTargetNode);
        data.IntegrationField[localTarget] = 0;

        // En la PriorityQueue guardamos siempre el GlobalID para pedir vecinos al grafo
        pq.Enqueue(globalTargetNode, 0);

        while (pq.Count > 0)
        {
            int currGlobal = pq.Dequeue();
            int currLocal = graph.GetLocalNode(currGlobal);
            float currDist = data.IntegrationField[currLocal];

            foreach (int neighborGlobal in graph.GetNeighbors(currGlobal))
            {
                // Solo vecinos de la misma región y caminables
                if (graph.GetRegionId(neighborGlobal) != regionId || !graph.IsWalkable(neighborGlobal))
                    continue;

                int neighborLocal = graph.GetLocalNode(neighborGlobal);
                float stepDist = graph.GetDistanceBetweenNeighbors(currGlobal, neighborGlobal);
                float newDist = currDist + (stepDist * graph.GetNodeCost(neighborGlobal));

                // Actualizamos el array local usando el índice local
                if (newDist < data.IntegrationField[neighborLocal])
                {
                    data.IntegrationField[neighborLocal] = newDist;
                    pq.Enqueue(neighborGlobal, newDist);
                }
            }
        }
    }

    private static void GenerateVectorField(INavGraph graph, int regionId, FlowField data)
    {
        // Solo iteramos sobre los nodos que caben en esta región
        // data.IntegrationField.Length es regW * regH
        for (int localIdx = 0; localIdx < data.IntegrationField.Length; localIdx++)
        {
            int globalIdx = graph.GetGlobalNode(localIdx, regionId);

            // Si el mapa no es múltiplo, GetGlobalNode devolverá -1 para nodos fuera de límites
            if (globalIdx == -1 || !graph.IsWalkable(globalIdx)) continue;
            if (data.IntegrationField[localIdx] == 0) continue; // Destino

            Vector3 currentPos = graph.GetNodePosition(globalIdx);
            Vector3 flowDir = Vector3.zero;
            float totalWeight = 0f;

            foreach (int neighborGlobal in graph.GetNeighbors(globalIdx))
            {
                if (graph.GetRegionId(neighborGlobal) != regionId) continue;

                int neighborLocal = graph.GetLocalNode(neighborGlobal);
                float costDiff = data.IntegrationField[localIdx] - data.IntegrationField[neighborLocal];

                if (costDiff > 0 && data.IntegrationField[neighborLocal] != float.MaxValue)
                {
                    Vector3 neighborPos = graph.GetNodePosition(neighborGlobal);
                    Vector3 dir = (neighborPos - currentPos).normalized;
                    flowDir += dir * costDiff;
                    totalWeight += costDiff;
                }
            }

            if (totalWeight > 0)
                data.FlowDirections[localIdx] = flowDir.normalized;
        }
    }
}