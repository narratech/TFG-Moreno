using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HierarchicalRouter
{
    private PortalGraph _portalGraph;
    private INavGraph _navGraph;

    public HierarchicalRouter(PortalGraph portalGraph, INavGraph navGraph)
    {
        _portalGraph = portalGraph;
        _navGraph = navGraph;
    }

    /// <summary>
    /// Devuelve un diccionario con la distancia mínima desde startPos a todos los portales alcanzables.
    /// </summary>
    public Dictionary<int, float> GetPortalDistanceField(int targetNode)
    {
        int targetRegion = _navGraph.GetRegionId(targetNode);

        // 1. FloodFill desde el destino hasta los portales de SU región
        float[] localDistances = PathFinder.RunFloodFill(_navGraph, targetNode, targetRegion);

        Dictionary<int, float> distanceMap = new Dictionary<int, float>();
        PriorityQueue<int, float> pq = new PriorityQueue<int, float>();

        // 2. Inicializar portales de la región del destino
        foreach (var portal in _portalGraph.GetAllPortals())
        {
            if (portal.RegionA == targetRegion || portal.RegionB == targetRegion)
            {
                int nodeInRegion = (portal.RegionA == targetRegion) ? portal.NodeA : portal.NodeB;
                float dist = localDistances[nodeInRegion];

                if (dist < float.MaxValue)
                {
                    distanceMap[portal.Id] = dist;
                    pq.Enqueue(portal.Id, dist);
                }
            }
        }

        // 3. Dijkstra Macro (Propagar distancias a todo el mundo)
        while (pq.Count > 0)
        {
            int currentId = pq.Dequeue();
            float currentDist = distanceMap[currentId];

            foreach (var edge in _portalGraph.GetNeighbors(currentId))
            {
                float newDist = currentDist + edge.Cost;
                if (!distanceMap.ContainsKey(edge.TargetPortalId) || newDist < distanceMap[edge.TargetPortalId])
                {
                    distanceMap[edge.TargetPortalId] = newDist;
                    pq.Enqueue(edge.TargetPortalId, newDist);
                }
            }
        }
        return distanceMap;
    }

    /// <summary>
    /// Toma el mapa de distancias abstractas ya consolidado y calcula la fase MÁXIMA de cada región
    /// hacia el destino, siguiendo estrictamente el gradiente descendente de distancias métricas.
    /// </summary>
    public Dictionary<int, int> CalculateRegionPhases(int targetRegion, Dictionary<int, float> distanceMap)
    {
        Dictionary<int, int> phases = new Dictionary<int, int>();

        // 1. Caso Base: El sumidero final siempre es la Fase 0
        phases[targetRegion] = 0;

        if (distanceMap == null || distanceMap.Count == 0)
            return phases;

        // Precalculamos la distancia mínima de cada región al destino para no repetir bucles
        Dictionary<int, float> regionDistances = new Dictionary<int, float>();
        regionDistances[targetRegion] = 0f;

        // 2. Extraer y filtrar todas las regiones que son físicamente alcanzables en esta ruta
        var reachableRegions = _portalGraph.GetAllPortals()
            .Where(p => distanceMap.ContainsKey(p.Id))
            .SelectMany(p => new[] { p.RegionA, p.RegionB })
            .Distinct()
            .Where(r => r != targetRegion)
            .ToList();

        // Rellenamos las distancias de las regiones usando los portales resueltos
        foreach (var r in reachableRegions)
        {
            regionDistances[r] = GetMinDistanceToRegion(r, distanceMap);
        }

        // Inicializamos las regiones alcanzables con una fase base (Fase 1)
        foreach (var r in reachableRegions)
        {
            phases[r] = 1;
        }

        // 3. Ordenamos las regiones por distancia métrica de MENOR a MAYOR.
        // Al procesarlas en este orden (desde el Target hacia afuera), garantizamos que 
        // las regiones más cercanas se asienten primero y empujen la fase hacia el exterior limpiamente.
        var sortedRegions = reachableRegions.OrderBy(r => regionDistances[r]).ToList();

        // 4. Propagación por gradiente de distancia
        bool changed = true;
        int maxIterations = sortedRegions.Count;

        for (int iter = 0; iter < maxIterations && changed; iter++)
        {
            changed = false;

            foreach (var portal in _portalGraph.GetAllPortals())
            {
                if (!distanceMap.ContainsKey(portal.Id)) continue;
                if (portal.RegionA == targetRegion && portal.RegionB == targetRegion) continue;

                float distA = regionDistances.ContainsKey(portal.RegionA) ? regionDistances[portal.RegionA] : 0f;
                float distB = regionDistances.ContainsKey(portal.RegionB) ? regionDistances[portal.RegionB] : 0f;

                int phaseA = phases.ContainsKey(portal.RegionA) ? phases[portal.RegionA] : 0;
                int phaseB = phases.ContainsKey(portal.RegionB) ? phases[portal.RegionB] : 0;

                // --- REGLA CRÍTICA DE GRADIENTE ---
                // Solo permitimos que la fase se propague si vamos en la dirección CORRECTA del flujo.
                // Si la Región A está MÁS LEJOS del destino que la Región B (distA > distB), 
                // significa que el camino viene de B hacia A. Por tanto, A debe ser, como mínimo, la fase de B + 1.

                if (portal.RegionA != targetRegion && distA > distB)
                {
                    if (phaseB + 1 > phaseA)
                    {
                        phases[portal.RegionA] = phaseB + 1;
                        changed = true;
                    }
                }

                // Lo mismo en sentido contrario (Si B está más lejos que A, el camino va de A hacia B)
                if (portal.RegionB != targetRegion && distB > distA)
                {
                    if (phaseA + 1 > phaseB)
                    {
                        phases[portal.RegionB] = phaseA + 1;
                        changed = true;
                    }
                }
            }
        }

        return phases;
    }

    /// <summary>
    /// Función auxiliar que encuentra la distancia mínima a la que se encuentra un portal de dicha región.
    /// </summary>
    private float GetMinDistanceToRegion(int regionId, Dictionary<int, float> distanceMap)
    {
        float minDist = float.MaxValue;
        foreach (var portal in _portalGraph.GetAllPortals())
        {
            if ((portal.RegionA == regionId || portal.RegionB == regionId) && distanceMap.TryGetValue(portal.Id, out float dist))
            {
                if (dist < minDist) minDist = dist;
            }
        }
        return minDist;
    }

    /// <summary>
    /// Dado un portal de inicio, la region destino y un distanceMap (resultado de GetPortalsDistancesFrom), devuelve la secuencia 
    /// de portales a tomar para llegar al destino.
    /// </summary>
    /// <param name="portal"></param>
    /// <param name="targetRegion"></param>
    /// <param name="distanceMap"></param>
    /// <returns></returns>
    public List<int> GetPathToDestination(int portal, int targetRegion, Dictionary<int, float> distanceMap)
    {
        List<int> path = new List<int>();
        int currentPortal = portal;
        while (currentPortal != -1)
        {
            path.Add(currentPortal);
            currentPortal = GetNextPortal(currentPortal, targetRegion, distanceMap);
        }
        return path;
    }

    /// <summary>
    /// Dado un portal actual y un distanceMap (resultado de GetPortalsDistancesFrom), devuelve el siguiente portal a 
    /// tomar para llegar al portal destino. Si el portal actual es el destino o no hay camino, devuelve -1.
    /// </summary>
    /// <param name="portal"></param>
    /// <param name="distanceMap"></param>
    /// <returns></returns>
    public int GetNextPortal(int portalId, int targetRegion, Dictionary<int, float> distanceMap)
    {
        int nextPortal = -1;
        float minDist = distanceMap.ContainsKey(portalId) ? distanceMap[portalId] : float.MaxValue;

        foreach (var edge in _portalGraph.GetNeighbors(portalId))
        {
            if (edge.RegionId == targetRegion) continue; // Si el portal esta en la region destino, no tine next portal
            if (distanceMap.TryGetValue(edge.TargetPortalId, out float neighborDist))
            {
                if (neighborDist < minDist)
                {
                    minDist = neighborDist;
                    nextPortal = edge.TargetPortalId;
                }
            }
        }
        return nextPortal;
    }

    /// <summary>
    /// Calcula la distancia mínima en "niveles de región" desde una región de origen a una de destino.
    /// Devuelve 0 si es la misma región, o int.MaxValue si no están conectadas.
    /// </summary>
    public int GetRegionDistance(int startRegionId, int targetRegionId)
    {
        if (startRegionId == targetRegionId) return 0;

        // Cola para almacenar (RegionId, Distancia/Nivel actual)
        Queue<System.ValueTuple<int, int>> queue = new Queue<System.ValueTuple<int, int>>();
        HashSet<int> visited = new HashSet<int>();

        queue.Enqueue((startRegionId, 0));
        visited.Add(startRegionId);

        while (queue.Count > 0)
        {
            var (currentRegion, currentLevel) = queue.Dequeue();

            // Obtenemos todos los portales de la región actual para ver a qué regiones vecinas conectan
            List<PortalNode> portalsInRegion = _portalGraph.GetPortalsInRegion(currentRegion);
            if (portalsInRegion == null) continue;

            foreach (var portal in portalsInRegion)
            {
                // Averiguamos cuál es la región del otro lado del portal
                int neighborRegion = (portal.RegionA == currentRegion) ? portal.RegionB : portal.RegionA;

                if (neighborRegion == targetRegionId)
                {
                    return currentLevel + 1;
                }

                if (!visited.Contains(neighborRegion))
                {
                    visited.Add(neighborRegion);
                    queue.Enqueue((neighborRegion, currentLevel + 1));
                }
            }
        }

        return int.MaxValue; // No hay conexión jerárquica entre las regiones
    }

    public List<PortalNode> SelectExitPortals(int regionId, int targetRegion, Dictionary<int, float> distanceMap)
    {
        List<PortalNode> allInRegion = _portalGraph.GetPortalsInRegion(regionId);
        List<PortalNode> exitPortals = new List<PortalNode>();

        foreach (var portal in allInRegion)
        {
            int nextPortalId = GetNextPortal(portal.Id, targetRegion, distanceMap);
            if (nextPortalId == -1)
            {
                exitPortals.Add(portal);
                continue;
            }
            PortalNode nextPortal = _portalGraph.GetPortal(nextPortalId);
            bool nextIsOutside = nextPortal.RegionA != regionId && nextPortal.RegionB != regionId;
            if (nextIsOutside)
            {
                exitPortals.Add(portal);
            }
        }
        return exitPortals;
    }

    public List<PortalNode> SelectEntryPortals(int regionId, int targetRegion, Dictionary<int, float> distanceMap)
    {
        List<PortalNode> allInRegion = _portalGraph.GetPortalsInRegion(regionId);
        List<PortalNode> entryPortals = new List<PortalNode>();
        foreach (var portal in allInRegion)
        {
            int nextPortalId = GetNextPortal(portal.Id, targetRegion, distanceMap);
            if (nextPortalId == -1) continue;
            PortalNode nextPortal = _portalGraph.GetPortal(nextPortalId);
            bool nextIsInside = nextPortal.RegionA == regionId || nextPortal.RegionB == regionId;
            if (nextIsInside)
            {
                entryPortals.Add(portal);
            }
        }
        return entryPortals;
    }
}