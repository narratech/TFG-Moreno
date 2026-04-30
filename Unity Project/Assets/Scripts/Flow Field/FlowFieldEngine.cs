using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static FlowFieldManager;

public static class FlowFieldEngine
{
    private static int NUM_REGIONLEVELS = 2; // Este valor indica cuantos niveles de flowfields de regiones genereamos en serie

    public static FlowField GetFlowFieldForDestination(INavGraph graph, int targetNode, int initialNode)
    {
        if (targetNode == -1 || initialNode == -1)
        {
            Debug.LogError("Invalid target or initial node for FlowField calculation.");
            return null;
        }
        FlowFieldManager manager = FlowFieldManager.Instance;
        int initialRegion = graph.GetRegionId(initialNode);
        int targetRegion = graph.GetRegionId(targetNode);

        if (!manager.TryGetRoute(graph, targetNode))
        {
            Debug.Log("No route cached, calculating new route for target node: " + targetNode);
            return null;
        }
        FlowFieldRoute route = manager.GetRoute(graph, targetNode);

        // Si la región donde está el agente ya tiene FlowField, lo devolvemos inmediatamente
        if (route.FlowFields.TryGetValue(initialRegion, out var cached)) return cached;

        // --- FASE 1: IDENTIFICACIÓN DE REGIONES ---
        HashSet<int> insideRegions = new HashSet<int>(); // Regiones que contienen el target o están dentro del rango de cálculo
        HashSet<int> frontierRegions = new HashSet<int> { initialRegion }; // Regiones que forman la frontera de cálculo, empezando por la inicial
        HierarchicalRouter router = manager.GetContext(graph).Router;
        var portalDistMap = route.DistanceMaps;

        for (int i = 0; i < NUM_REGIONLEVELS; i++)
        {
            HashSet<int> nextIterationRegs = new HashSet<int>();
            foreach (int rid in frontierRegions)
            {
                if (insideRegions.Contains(rid)) continue; // Si ya sabemos que esta región está dentro, no la procesamos

                if (route.FlowFields.ContainsKey(rid))
                {
                    nextIterationRegs.Add(rid);
                    continue;
                }

                if (rid == targetRegion) continue;
                foreach (int nextRid in GetNextRegions(graph, rid, portalDistMap, router))
                {
                    nextIterationRegs.Add(nextRid);
                }

                insideRegions.Add(rid);
            }
            frontierRegions = nextIterationRegs;
        }

        // --- FASE 2: PREPARACIÓN DE SUMIDEROS (SINKS) ---
        Dictionary<int, float> destinations = new Dictionary<int, float>();

        // Portales de entrada de las regiones "frontera" para dar continuidad
        foreach (int rid in frontierRegions)
        {
            if (rid == targetRegion)
            {
                destinations[targetNode] = 0f;
                continue;
            }
            List<PortalNode> entryPortals = router.SelectEntryPortals(rid, portalDistMap);
            foreach (var portal in entryPortals)
            {
                int node = portal.RegionA == rid ? portal.NodeA : portal.NodeB;
                destinations[node] = portalDistMap[portal.Id];
            }
        }

        // --- FASE 3: CÁLCULO ---
        Dictionary<int, FlowField> regionDataMap = new Dictionary<int, FlowField>();
        // Necesitamos crear FlowFields tanto para las nuevas como para las frontera para que Dijkstra fluya
        HashSet<int> allRelevantRegs = new HashSet<int>(insideRegions);
        foreach (int rid in frontierRegions) allRelevantRegs.Add(rid);

        foreach (int rid in allRelevantRegs)
            regionDataMap[rid] = new FlowField(graph.GetRegionSize(rid), rid);

        GenerateIntegartionFields(graph, allRelevantRegs, destinations, regionDataMap);
        GenerateVectorFields(graph, regionDataMap);

        // --- FASE 4: PERSISTENCIA ---
        foreach (int rid in insideRegions)
        {

            route.FlowFields[rid] = regionDataMap[rid];
        }

        return route.FlowFields[initialRegion];
    }

    private static List<int> GetNextRegions(INavGraph graph, int regionId, Dictionary<int, float> distanceMap, HierarchicalRouter router)
    {
        List<int> nextRegions = new List<int>();
        List<PortalNode> portals = router.SelectExitPortals(regionId, distanceMap);
        foreach (var portal in portals)
        {
            int nextRid = portal.RegionA == regionId ? portal.RegionB : portal.RegionA;
            if (!nextRegions.Contains(nextRid)) nextRegions.Add(nextRid);
        }
        return nextRegions;
    }

    private static void GenerateIntegartionFields(
        INavGraph graph,
        HashSet<int> regionIds,
        Dictionary<int, float> destinations,
        Dictionary<int, FlowField> regionDataMap)
    {
        // 1. Setup para búsqueda rápida de validez de región
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        // 2. Sembrar los puntos de destino (Sinks)
        foreach (var kvp in destinations)
        {
            int globalNode = kvp.Key;
            float initialCost = kvp.Value;
            int rId = graph.GetRegionId(globalNode);

            // Solo sembramos si el nodo pertenece a nuestras regiones de interés
            if (regionIds.Contains(rId))
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
                if (!regionIds.Contains(nRegion) || !graph.IsWalkable(neighborGlobal))
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