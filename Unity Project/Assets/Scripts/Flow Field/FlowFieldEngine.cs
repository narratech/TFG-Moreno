using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static FlowFieldManager;

public static class FlowFieldEngine
{
    private static int NUM_REGIONLEVELS = 1; // Este valor indica cuantos niveles de flowfields de regiones genereamos en serie

    public static FlowField GetFlowFieldForDestination(
        INavGraph graph,
        int targetNode,
        int initalNode)
    {
        // Verificar si hay Ruta directa sin necesidad de generar flowfield
        FlowFieldManager manager = FlowFieldManager.Instance;
        FlowFieldRoute route = null;
        if (!manager.TryGetRoute(graph, targetNode))
        {
            manager.RegisterRoute(graph, targetNode);
        }
        route = manager.GetRoute(graph, targetNode);

        if (route.FlowFields.ContainsKey(graph.GetRegionId(initalNode)))
        {
            return route.FlowFields[graph.GetRegionId(initalNode)];
        }

        // Generar FlowFields para el destino dado
        // 1. Identificar las regiones relevantes para el nodo destino y la cantidad de niveles que queremos generar

        List<int> regionIds = new List<int>();
        List<int> lastRegionIds = new List<int>();
        List<int> portalIds = new List<int>();

        Dictionary<int, float> portalDistMap = route.DistanceMaps;


        // Generamos camposde integración para cada región relevante
        NavContext context = manager.GetContext(graph);
        PortalGraph portalGraph = context.PortalGraph;
        Dictionary<int, FlowField> regionDataMap = new Dictionary<int, FlowField>();
        List<int> allRegionIds = regionIds + lastRegionIds;
        Dictionary<int, float> destinantions = new Dictionary<int, float>();
        foreach (var item in portalIds)
        {
            PortalNode pn = portalGraph.GetPortal(item);
            int regIdA = pn.RegionA;
            int regIdB = pn.RegionB;
            
            destinantions[item] = portalDistMap[item];
        }
        GenerateIntegartionFields(graph, allRegionIds, destinantions, regionDataMap);

    }

    private static void GenerateIntegartionFields(
        INavGraph graph,
        List<int> regionIds,
        Dictionary<int, float> destinations,
        Dictionary<int, FlowField> regionDataMap)
    {
        // 1. Setup para búsqueda rápida de validez de región
        HashSet<int> activeRegions = new HashSet<int>(regionIds);
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        // 2. Sembrar los puntos de destino (Sinks)
        foreach (var kvp in destinations)
        {
            int globalNode = kvp.Key;
            float initialCost = kvp.Value;
            int rId = graph.GetRegionId(globalNode);

            // Solo sembramos si el nodo pertenece a nuestras regiones de interés
            if (activeRegions.Contains(rId))
            {
                int localIdx = graph.GetLocalNode(globalNode);
                regionDataMap[rId].IntegrationField[localIdx] = initialCost;
                pq.Enqueue(globalNode, initialCost);
            }
        }

        // 3. Algoritmo de Dijkstra Multi-Región
        while (pq.Count > 0)
        {
            int currGlobal = pq.Dequeue();
            int currRegion = graph.GetRegionId(currGlobal);
            int currLocal = graph.GetLocalNode(currGlobal);
            float currDist = regionDataMap[currRegion].IntegrationField[currLocal];

            foreach (int neighborGlobal in graph.GetNeighbors(currGlobal))
            {
                int nRegion = graph.GetRegionId(neighborGlobal);

                // Regla de Oro: Solo expandir si el vecino está en el set de regiones 
                // que estamos calculando y es caminable.
                if (!activeRegions.Contains(nRegion) || !graph.IsWalkable(neighborGlobal))
                    continue;

                int nLocal = graph.GetLocalNode(neighborGlobal);
                float stepDist = graph.GetDistanceBetweenNeighbors(currGlobal, neighborGlobal);
                float newDist = currDist + (stepDist * graph.GetNodeCost(neighborGlobal));

                // Si encontramos un camino más corto hacia este nodo
                if (newDist < regionDataMap[nRegion].IntegrationField[nLocal])
                {
                    regionDataMap[nRegion].IntegrationField[nLocal] = newDist;
                    pq.Enqueue(neighborGlobal, newDist);
                }
            }
        }
    }

    private static void GenerateVectorFields(INavGraph graph, Dictionary<int, FlowField> regionDataMap)
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