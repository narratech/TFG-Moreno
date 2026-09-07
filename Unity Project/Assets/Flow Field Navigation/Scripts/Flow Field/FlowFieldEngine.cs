using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static FlowFieldManager;

public static class FlowFieldEngine
{
    private static int NUM_REGIONLEVELS = 2; // Este valor indica cuantos niveles de flowfields de regiones generamos en serie

    public static FlowField GenerateFlowPath(INavGraph graph, int targetNode, int initialRegion)
    {
        if (targetNode == -1 || initialRegion == -1)
        {
            Debug.LogError("Invalid target or initial region for FlowField calculation.");
            return null;
        }
        if (graph == null)
        {
            Debug.LogError("Invalid NavGraph for FlowField calculation.");
            return null;
        }
        if (!graph.IsWalkable(targetNode))
        {
            Debug.LogWarning("Target position not walkable. Invalid for FlowField calculation.");
            return null;
        }
        FlowFieldManager manager = FlowFieldManager.Instance;

        int targetRegion = graph.GetRegionId(targetNode);

        if (!manager.TryGetRoute(graph, targetNode))
        {
            Debug.Log("No route cached, calculating new route for target node: " + targetNode);
            manager.RegisterRoute(graph, targetNode);
        }
        FlowFieldRoute route = manager.GetRoute(graph, targetNode);

        // Si la región donde está el agente ya tiene FlowField, lo devolvemos inmediatamente
        if (route.FlowFields.TryGetValue(initialRegion, out var cached)) return cached;

        // --- FASE 1: IDENTIFICACIÓN DE REGIONES ---
        HashSet<int> insideRegions = new HashSet<int>(); 
        HashSet<int> frontierRegions = new HashSet<int> { initialRegion }; 
        HierarchicalRouter router = manager.GetContext(graph).Router;
        var portalDistMap = route.DistanceMaps;

        for (int i = 0; i < NUM_REGIONLEVELS; i++)
        {
            HashSet<int> nextIterationRegs = new HashSet<int>();
            foreach (int rid in frontierRegions)
            {
                if (insideRegions.Contains(rid)) continue;

                if (route.FlowFields.ContainsKey(rid))
                {
                    nextIterationRegs.Add(rid);
                    continue;
                }

                if (rid == targetRegion)
                {
                    insideRegions.Add(rid);
                    continue;
                }

                foreach (int nextRid in GetNextRegions(graph, rid, targetRegion, portalDistMap, router))
                {
                    nextIterationRegs.Add(nextRid);
                }

                insideRegions.Add(rid);
            }
            frontierRegions = nextIterationRegs;
        }

        // --- FASE 2: SUMIDEROS ---
        Dictionary<int, float> destinations = new Dictionary<int, float>();

        if (insideRegions.Contains(targetRegion))
        {
            destinations[targetNode] = 0f;
        }

        foreach (int rid in frontierRegions)
        {
            if (rid == targetRegion) destinations[targetNode] = 0f;

            List<PortalNode> entryPortals = router.SelectExitPortals(rid, targetRegion, portalDistMap);
            foreach (var portal in entryPortals)
            {
                int node = portal.RegionA == rid ? portal.NodeA : portal.NodeB;
                destinations[node] = portalDistMap[portal.Id];
            }
        }

        if (destinations.Count == 0)
        {
            Debug.LogError("No valid destinations found for FlowField calculation. Check if the target is reachable.");
            return null;
        }

        // --- FASE 3: CÁLCULO ---
        Dictionary<int, FlowField> regionDataMap = new Dictionary<int, FlowField>();
        HashSet<int> allRelevantRegs = new HashSet<int>(insideRegions);
        foreach (int rid in frontierRegions) allRelevantRegs.Add(rid);

        foreach (int rid in allRelevantRegs)
            regionDataMap[rid] = new FlowField(graph.GetRegionSize(rid), rid);

        GenerateIntegrationFields(graph, allRelevantRegs, destinations, regionDataMap);
        GenerateVectorFields(graph, regionDataMap, targetNode);

        // --- FASE 4: PERSISTENCIA ---
        foreach (int rid in insideRegions)
        {
            route.FlowFields[rid] = regionDataMap[rid];
            FlowFieldStorage.Instance.Register(new FlowFieldKey(graph.GraphId, targetNode, rid), regionDataMap[rid]);
        }

        return route.FlowFields[initialRegion];
    }

    private static List<int> GetNextRegions(
        INavGraph graph, 
        int regionId, 
        int targetRegion,
        Dictionary<int, float> distanceMap, 
        HierarchicalRouter router)
    {
        List<int> nextRegions = new List<int>();
        List<PortalNode> portals = router.SelectExitPortals(regionId, targetRegion, distanceMap);
        foreach (var portal in portals)
        {
            int nextRid = portal.RegionA == regionId ? portal.RegionB : portal.RegionA;
            if (!nextRegions.Contains(nextRid)) nextRegions.Add(nextRid);
        }
        return nextRegions;
    }

    public struct NeighborData
    {
        public Vector3 Pos;
        public float T;
        public float Cost;

        public NeighborData(Vector3 pos, float t, float cost = 1.0f)
        {
            this.Pos = pos;
            this.T = t;
            this.Cost = cost;
        }
    }

    private static void GenerateIntegrationFields(
        INavGraph graph,
        HashSet<int> regionIds,
        Dictionary<int, float> destinations,
        Dictionary<int, FlowField> regionDataMap)
    {
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        foreach (var kvp in destinations)
        {
            int globalNode = kvp.Key;
            if (regionIds.Contains(graph.GetRegionId(globalNode)))
            {
                int rId = graph.GetRegionId(globalNode);
                int localIdx = graph.GetLocalNode(globalNode);
                regionDataMap[rId].IntegrationField[localIdx] = kvp.Value;
                pq.Enqueue(globalNode, kvp.Value);
            }
        }

        while (pq.Count > 0)
        {
            int currGlobal = pq.Dequeue();

            foreach (int neighborGlobal in graph.GetNeighbors(currGlobal))
            {
                int nRegion = graph.GetRegionId(neighborGlobal);

                if (!regionIds.Contains(nRegion) || !graph.IsWalkable(neighborGlobal))
                    continue;

                List<NeighborData> acceptedNeighbors = new List<NeighborData>();

                foreach (int nOfN in graph.GetNeighbors(neighborGlobal))
                {
                    int nnRegion = graph.GetRegionId(nOfN);

                    if (!regionIds.Contains(nnRegion) || !graph.IsWalkable(nOfN))
                        continue;

                    int nnLocal = graph.GetLocalNode(nOfN);
                    float val = regionDataMap[nnRegion].IntegrationField[nnLocal];

                    if (val < float.MaxValue)
                    {
                        acceptedNeighbors.Add(new NeighborData(
                            graph.GetNodePosition(nOfN),
                            val,
                            graph.GetNodeCost(nOfN)
                        ));
                    }
                }

                float nodeCost = graph.GetNodeCost(neighborGlobal);
                Vector3 targetPos = graph.GetNodePosition(neighborGlobal);

                if (acceptedNeighbors.Count == 0) continue;

                float newDist = CalculateEikonalCost(targetPos, acceptedNeighbors, nodeCost);

                int nLocal = graph.GetLocalNode(neighborGlobal);
                if (newDist < regionDataMap[nRegion].IntegrationField[nLocal])
                {
                    regionDataMap[nRegion].IntegrationField[nLocal] = newDist;
                    pq.Enqueue(neighborGlobal, newDist);
                }
            }
        }
    }

    private static float CalculateEikonalCost(Vector3 targetPos, List<NeighborData> neighbors, float localCost)
    {
        var sorted = neighbors.OrderBy(n => n.T).ToList();

        if (sorted.Count >= 3)
        {
            float t = SolveQuadraticND(targetPos, sorted.Take(3).ToList(), localCost);
            if (!float.IsNaN(t) && IsCausal(t, sorted.Take(3).ToList(), targetPos))
                return t;
        }

        if (sorted.Count >= 2)
        {
            float t = SolveQuadraticND(targetPos, sorted.Take(2).ToList(), localCost);
            if (!float.IsNaN(t) && IsCausal(t, sorted.Take(2).ToList(), targetPos))
                return t;
        }

        return sorted[0].T + (Vector3.Distance(targetPos, sorted[0].Pos) * localCost);
    }

    private static float SolveQuadraticND(Vector3 pC, List<NeighborData> pts, float f)
    {
        int n = pts.Count;
        Vector3[] v = new Vector3[n];
        float[] t = new float[n];

        for (int i = 0; i < n; i++)
        {
            v[i] = pts[i].Pos - pC;
            t[i] = pts[i].T;
        }

        float a = 0, b = 0, c = -f * f;

        for (int i = 0; i < n; i++)
        {
            float d = v[i].magnitude;
            a += 1 / (d * d);
            b -= 2 * t[i] / (d * d);
            c += (t[i] * t[i]) / (d * d);
        }

        float disc = b * b - 4 * a * c;
        if (disc < 0) return float.NaN;

        return (-b + MathF.Sqrt(disc)) / (2 * a);
    }

    private static bool IsCausal(float potential, List<NeighborData> pts, Vector3 pC)
    {
        foreach (var p in pts)
        {
            if (potential <= p.T) return false;
        }
        return true;
    }

    private static void GenerateVectorFields(INavGraph graph, Dictionary<int, FlowField> regionDataMap, int targetNode)
    {
        HashSet<int> regionSet = new HashSet<int>(regionDataMap.Keys);
        int targetRegion = graph.GetRegionId(targetNode);
        int targetLocal = graph.GetLocalNode(targetNode);

        foreach (var kvp in regionDataMap)
        {
            int regionId = kvp.Key;
            FlowField data = kvp.Value;

            for (int localIdx = 0; localIdx < data.IntegrationField.Length; localIdx++)
            {
                int globalIdx = graph.GetGlobalNode(localIdx, regionId);
                if (globalIdx == -1 || !graph.IsWalkable(globalIdx)) continue;

                // Si es exactamente el nodo objetivo, su vector de flujo debe apuntar a cero para facilitar la detención
                if (regionId == targetRegion && localIdx == targetLocal)
                {
                    data.FlowDirections[localIdx] = Vector3.zero;
                    continue;
                }

                Vector3 currentPos = graph.GetNodePosition(globalIdx);
                Vector3 flowDir = Vector3.zero;
                float totalWeight = 0f;

                foreach (int neighborGlobal in graph.GetNeighbors(globalIdx))
                {
                    int nRegionId = graph.GetRegionId(neighborGlobal);

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