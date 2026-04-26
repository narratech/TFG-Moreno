using System.Collections.Generic;
using UnityEngine;

public static class FlowFieldEngine
{
    public static void CalculateFlowField(INavGraph graph, int regionId, int targetNode)
    {
        // Creamos un mapa de una sola región para reutilizar la lógica multi
        Dictionary<int, FlowField> regionDataMap = new Dictionary<int, FlowField>
        {
            { regionId, new FlowField(graph.GetRegionSize(regionId), regionId) }
        };

        GenerateMultiIntegrationField(graph, new List<int> { regionId }, targetNode, regionDataMap);
        GenerateMultiVectorField(graph, regionDataMap);

        // Guardamos en memoria
        FlowFieldManager manager = FlowFieldManager.Instance;
        if (manager.TryGetContext(graph))
        {
            var context = manager.GetContext(graph);
            context.FlowFieldCache[(regionId, targetNode)] = regionDataMap[regionId];
        }
    }

    public static bool CalculateMultiFlowField(INavGraph graph, List<int> regionIds, int targetNode)
    {
        FlowFieldManager manager = FlowFieldManager.Instance;
        if (graph == null)
        {
            Debug.LogError("El NavGraph proporcionado es nulo.");
            return false;
        }
        if (!manager.TryGetContext(graph)) return false;

        // 1. Inicialización
        Dictionary<int, FlowField> regionDataMap = new Dictionary<int, FlowField>();
        foreach (int rid in regionIds)
        {
            regionDataMap[rid] = new FlowField(graph.GetRegionSize(rid), rid);
        }

        // Seguridad: ¿El target está en las regiones pedidas?
        int targetRegion = graph.GetRegionId(targetNode);
        if (!regionDataMap.ContainsKey(targetRegion))
        {
            Debug.LogError("El TargetNode no pertenece a ninguna de las regiones proporcionadas.");
            return false;
        }

        // 2. Ejecución de algoritmos
        GenerateMultiIntegrationField(graph, regionIds, targetNode, regionDataMap);
        GenerateMultiVectorField(graph, regionDataMap);

        // 3. Persistencia en Memoria
        var context = manager.GetContext(graph);
        foreach (var kvp in regionDataMap)
        {
            context.FlowFieldCache[(kvp.Key, targetNode)] = kvp.Value;
        }

        return true;
    }

    public static void GenerateMultiIntegrationField(INavGraph graph, List<int> regionIds, int globalTargetNode, Dictionary<int, FlowField> regionDataMap)
    {
        HashSet<int> regionSet = new HashSet<int>(regionDataMap.Keys);

        // 2. Dijkstra Multi-Región
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        // El target está en una de las regiones. Lo inicializamos.
        int targetRegion = graph.GetRegionId(globalTargetNode);
        int localTarget = graph.GetLocalNode(globalTargetNode);
        regionDataMap[targetRegion].IntegrationField[localTarget] = 0;
        pq.Enqueue(globalTargetNode, 0);

        while (pq.Count > 0)
        {
            int currGlobal = pq.Dequeue();
            int currRegion = graph.GetRegionId(currGlobal);
            int currLocal = graph.GetLocalNode(currGlobal);
            float currDist = regionDataMap[currRegion].IntegrationField[currLocal];

            foreach (int neighborGlobal in graph.GetNeighbors(currGlobal))
            {
                int neighborRegion = graph.GetRegionId(neighborGlobal);

                if (!regionSet.Contains(neighborRegion) || !graph.IsWalkable(neighborGlobal))
                    continue;

                int neighborLocal = graph.GetLocalNode(neighborGlobal);
                float stepDist = graph.GetDistanceBetweenNeighbors(currGlobal, neighborGlobal);
                float newDist = currDist + (stepDist * graph.GetNodeCost(neighborGlobal));

                if (newDist < regionDataMap[neighborRegion].IntegrationField[neighborLocal])
                {
                    regionDataMap[neighborRegion].IntegrationField[neighborLocal] = newDist;
                    pq.Enqueue(neighborGlobal, newDist);
                }
            }
        }
    }

    private static void GenerateMultiVectorField(INavGraph graph, Dictionary<int, FlowField> regionDataMap)
    {
        // Creamos el set de regiones una sola vez para búsqueda rápida O(1)
        HashSet<int> regionSet = new HashSet<int>(regionDataMap.Keys);

        // Iteramos por cada par Región-FlowField en nuestro set
        foreach (var kvp in regionDataMap)
        {
            int regionId = kvp.Key;
            FlowField data = kvp.Value;

            for (int localIdx = 0; localIdx < data.IntegrationField.Length; localIdx++)
            {
                int globalIdx = graph.GetGlobalNode(localIdx, regionId);
                if (globalIdx == -1 || !graph.IsWalkable(globalIdx)) continue;
                if (data.IntegrationField[localIdx] == 0) continue;

                Vector3 currentPos = graph.GetNodePosition(globalIdx);
                Vector3 flowDir = Vector3.zero;
                float totalWeight = 0f;

                foreach (int neighborGlobal in graph.GetNeighbors(globalIdx))
                {
                    int nRegionId = graph.GetRegionId(neighborGlobal);

                    // Si el vecino pertenece a alguna de las regiones del Multi-FF
                    if (regionSet.Contains(nRegionId))
                    {
                        int nLocal = graph.GetLocalNode(neighborGlobal);
                        float nCost = regionDataMap[nRegionId].IntegrationField[nLocal];

                        float costDiff = data.IntegrationField[localIdx] - nCost;

                        if (costDiff > 0 && nCost != float.MaxValue)
                        {
                            Vector3 dir = (graph.GetNodePosition(neighborGlobal) - currentPos).normalized;
                            flowDir += dir * costDiff;
                            totalWeight += costDiff;
                        }
                    }
                }

                if (totalWeight > 0)
                    data.FlowDirections[localIdx] = flowDir.normalized;
            }
        }
    }
}