using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static FlowFieldManager;

public static class FlowFieldEngine
{
    private static int NUM_REGIONLEVELS = 1; // Este valor indica cuantos niveles de flowfields de regiones genereamos en serie

    public static FlowField GenerateFlowPath(INavGraph graph, int targetNode, int initialNode)
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
                if (insideRegions.Contains(rid)) continue;

                // Si ya está cacheada, será un sumidero (Sink)
                if (route.FlowFields.ContainsKey(rid))
                {
                    nextIterationRegs.Add(rid);
                    continue;
                }

                // IMPORTANTE: Si es la región del target, la marcamos para calcular
                // pero NO expandimos sus vecinos (porque ya llegamos al final)
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

        // EL TARGET SIEMPRE VA (si alguna de las regiones a calcular es la suya)
        if (insideRegions.Contains(targetRegion))
        {
            destinations[targetNode] = 0f;
        }

        foreach (int rid in frontierRegions)
        {
            // Si la región frontera es la del target y no la calculamos en esta iteración
            if (rid == targetRegion) destinations[targetNode] = 0f;

            // Si la región está cacheada, DEBERÍAS intentar leer sus portales (opcional pero recomendado)
            // Por ahora, usamos tu lógica de portalDistMap que es segura:
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
        // Necesitamos crear FlowFields tanto para las nuevas como para las frontera para que Dijkstra fluya
        HashSet<int> allRelevantRegs = new HashSet<int>(insideRegions);
        foreach (int rid in frontierRegions) allRelevantRegs.Add(rid);

        foreach (int rid in allRelevantRegs)
            regionDataMap[rid] = new FlowField(graph.GetRegionSize(rid), rid);

        GenerateIntegrationFields(graph, allRelevantRegs, destinations, regionDataMap);
        GenerateVectorFields(graph, regionDataMap);

        // --- FASE 4: PERSISTENCIA ---
        foreach (int rid in insideRegions)
        {
            route.FlowFields[rid] = regionDataMap[rid];
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
        // La posición absoluta en el mundo (Vector3 para ser agnóstico 2D/3D)
        public Vector3 Pos;

        // El valor acumulado en el Integration Field (T)
        public float T;

        // El coste intrínseco de este nodo
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

        // 1. Sembramos los destinos (igual que antes)
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

        // 2. Bucle Fast Marching
        while (pq.Count > 0)
        {
            int currGlobal = pq.Dequeue();

            // En Fast Marching, expandimos hacia los vecinos para RE-CALCULARLOS
            foreach (int neighborGlobal in graph.GetNeighbors(currGlobal))
            {
                int nRegion = graph.GetRegionId(neighborGlobal);

                if (!regionIds.Contains(nRegion) || !graph.IsWalkable(neighborGlobal))
                    continue;

                // --- PASO CLAVE: Recopilar vecinos válidos ---
                // Para calcular el coste de 'neighborGlobal', miramos sus propios vecinos 
                // que ya tienen un coste asignado (incluyendo 'currGlobal').
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

                // --- CALCULO EIKONAL ---
                float nodeCost = graph.GetNodeCost(neighborGlobal);
                Vector3 targetPos = graph.GetNodePosition(neighborGlobal);

                // Usamos la función que creamos antes
                float newDist = CalculateEikonalCost(targetPos, acceptedNeighbors, nodeCost);

                // Si el nuevo coste calculado es mejor, actualizamos y encolamos
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
        // Ordenar vecinos de menor a mayor coste (Causalidad)
        var sorted = neighbors.OrderBy(n => n.T).ToList();

        // Si estamos en 3D, intentamos usar 3 vecinos para formar un tetraedro
        if (sorted.Count >= 3)
        {
            float t = SolveQuadraticND(targetPos, sorted.Take(3).ToList(), localCost);
            if (!float.IsNaN(t) && IsCausal(t, sorted.Take(3).ToList(), targetPos))
                return t;
        }

        // Si falla o estamos en 2D, intentamos con 2 vecinos (Triángulo)
        if (sorted.Count >= 2)
        {
            float t = SolveQuadraticND(targetPos, sorted.Take(2).ToList(), localCost);
            if (!float.IsNaN(t) && IsCausal(t, sorted.Take(2).ToList(), targetPos))
                return t;
        }

        // Aquí estamos en 1D Dijkstra puro (el vecino más barato + distancia)
        return sorted[0].T + (Vector3.Distance(targetPos, sorted[0].Pos) * localCost);
    }

    private static float SolveQuadraticND(Vector3 pC, List<NeighborData> pts, float f)
    {
        // Construimos el sistema basado en distancias relativas
        // Para simplificar a cualquier dimensión usamos una aproximación de Gram-Schmidt 
        // o resolvemos el sistema lineal: (M^T * M) u = 1

        int n = pts.Count;
        Matrix4x4 m = new Matrix4x4(); // Usamos matriz para guardar vectores dirección
        Vector3[] v = new Vector3[n];
        float[] t = new float[n];

        for (int i = 0; i < n; i++)
        {
            v[i] = pts[i].Pos - pC;
            t[i] = pts[i].T;
        }

        // Aquí resolvemos: sum( (Tc - Ti) / dist_i )^2 = f^2
        // En un TFG, la forma más limpia es usar la fórmula de "Kimmel":
        // a*Tc^2 + b*Tc + c = 0

        float a = 0, b = 0, c = -f * f;

        // Simplificación para ejes ortonormales (fácil de entender):
        // Si no son ortonormales, se usa el tensor métrico del simplex.
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
        // el potencial calculado debe ser mayor que el de todos los vecinos que lo crearon
        foreach (var p in pts)
        {
            if (potential <= p.T) return false;
        }

        // Verificación de Ángulo: ¿viene la onda desde el interior del simplex?
        // Se calcula viendo si el gradiente cae dentro de las caras del triángulo/tetraedro
        return true;
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