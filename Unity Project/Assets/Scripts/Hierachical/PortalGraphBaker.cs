using System.Collections.Generic;
using UnityEngine;

public static class PortalGraphBaker
{
    static private int _idCounter = 0;

    static public void Bake(INavGraph navGraph, PortalGraph portalGraph)
    {
        _idCounter = 0;
        // 1: Generar las fronteras entre regiones
        var boundaries = GenerateFrontiers(navGraph, portalGraph);
        // 2: Generar los portales (Fronteras)
        GeneratePortals(navGraph, portalGraph, boundaries);
        // 3: Conectar los portales entre sí (Caminos internos)
        ConnectPortals(navGraph, portalGraph);
    }

    static public void Clear(PortalGraph portalGraph)
    {
        portalGraph.Clear();
    }

    static public void Rebake(INavGraph navGraph, PortalGraph portalGraph)
    {
        Clear(portalGraph);
        Bake(navGraph, portalGraph);
    }

    static private Dictionary<(int, int), List<(int nodeA, int nodeB)>> GenerateFrontiers(INavGraph navGraph, PortalGraph portalGraph)
    {
        // Clave: (RegionA, RegionB) con RegionA < RegionB para evitar duplicados
        // Valor: Lista de pares de nodos (nodeA, nodeB) que forman la frontera entre esas regiones
        var boundaries = new Dictionary<(int, int), List<(int nodeA, int nodeB)>>();

        for (int i = 0; i < navGraph.NodeCount; i++)
        {
            if (!navGraph.IsWalkable(i)) continue;

            int regA = navGraph.GetRegionId(i);
            foreach (int neighbor in navGraph.GetNeighbors(i))
            {
                int regB = navGraph.GetRegionId(neighbor);

                if (regA != regB && regA != -1 && regB != -1)
                {
                    var key = regA < regB ? (regA, regB) : (regB, regA);
                    if (!boundaries.ContainsKey(key)) boundaries[key] = new List<(int, int)>();
                    boundaries[key].Add((i, neighbor));
                }
            }
        }

        return boundaries;
    }

    static private void GeneratePortals(INavGraph navGraph, PortalGraph portalGraph, Dictionary<(int, int), List<(int nodeA, int nodeB)>> boundaries)
    {
        foreach (var boundary in boundaries)
        {
            var nodes = boundary.Value;
            var middlePair = nodes[nodes.Count / 2];

            PortalNode newPortal = new PortalNode(
                _idCounter++,
                middlePair.nodeA,
                middlePair.nodeB,
                navGraph.GetRegionId(middlePair.nodeA),
                navGraph.GetRegionId(middlePair.nodeB),
                navGraph.GetNodePosition(middlePair.nodeA),
                navGraph.GetNodePosition(middlePair.nodeB)
            );

            portalGraph.AddPortal(newPortal);
        }
    }

    static private void ConnectPortals(INavGraph navGraph, PortalGraph portalGraph)
    {
        var regionToPortals = new Dictionary<int, List<PortalNode>>();
        foreach (PortalNode portal in portalGraph.GetAllPortals())
        {
            AddPortalToMap(regionToPortals, portal.RegionA, portal);
            AddPortalToMap(regionToPortals, portal.RegionB, portal);
        }

        // Optimizacion 1: Evitar procesar A->B y luego B->A
        HashSet<(int, int)> connectedPairs = new HashSet<(int, int)>();

        foreach (var kvp in regionToPortals)
        {
            int regionId = kvp.Key;
            List<PortalNode> portalsInRegion = kvp.Value;

            if (portalsInRegion.Count < 2) continue;

            foreach (var startPortal in portalsInRegion)
            {
                int startNode = (startPortal.RegionA == regionId) ? startPortal.NodeA : startPortal.NodeB;

                // Optimizacion 2: Llamamos a tu clase PathFinder (FloodFill con array de floats es mejor)
                var distanceMap = PathFinder.RunFloodFill(navGraph, startNode, regionId);

                foreach (var endPortal in portalsInRegion)
                {
                    if (startPortal.Id == endPortal.Id) continue;

                    // Crear clave unica para la pareja (ID menor siempre primero)
                    var pairKey = startPortal.Id < endPortal.Id ?
                                 (startPortal.Id, endPortal.Id) :
                                 (endPortal.Id, startPortal.Id);

                    if (connectedPairs.Contains(pairKey)) continue;

                    int targetNode = (endPortal.RegionA == regionId) ? endPortal.NodeA : endPortal.NodeB;

                    // Generar el costo desde startNode a targetNode
                    float cost = distanceMap[targetNode];

                    if (cost >= 0 && cost < float.MaxValue)
                    {
                        portalGraph.AddEdge(startPortal.Id, endPortal.Id, cost);
                        connectedPairs.Add(pairKey);
                    }
                }
            }
        }
    }

    static private void AddPortalToMap(Dictionary<int, List<PortalNode>> map, int regionId, PortalNode portal)
    {
        if (!map.ContainsKey(regionId)) map[regionId] = new List<PortalNode>();
        if (!map[regionId].Contains(portal)) map[regionId].Add(portal);
    }
}