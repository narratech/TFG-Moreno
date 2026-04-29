using System.Collections.Generic;
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
    /// Dado un portal de inicio y un distanceMap (resultado de GetPortalsDistancesFrom), devuelve la secuencia 
    /// de portales a tomar para llegar al destino.
    /// </summary>
    /// <param name="portal"></param>
    /// <param name="distanceMap"></param>
    /// <returns></returns>
    public List<int> GetPathToDestination(int portal, Dictionary<int, float> distanceMap)
    {
        List<int> path = new List<int>();
        int currentPortal = portal;
        while (currentPortal != -1)
        {
            path.Add(currentPortal);
            currentPortal = GetNextPortal(currentPortal, distanceMap);
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
    public int GetNextPortal(int portalId, Dictionary<int, float> distanceMap)
    {
        int nextPortal = -1;
        float minDist = distanceMap.ContainsKey(portalId) ? distanceMap[portalId] : float.MaxValue;

        foreach (var edge in _portalGraph.GetNeighbors(portalId))
        {
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
}