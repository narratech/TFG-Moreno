using System.Collections.Generic;
using UnityEngine;

public class PortalGraphBaker
{
    private int _idCounter = 0;

    public void Bake(INavGraph navGraph, PortalGraph portalGraph)
    {
        _idCounter = 0;
        // 1: Generar las fronteras entre regiones
        var boundaries = GenerateFrontiers(navGraph, portalGraph);
        // 2: Generar los portales (Fronteras)
        GeneratePortals(navGraph, portalGraph, boundaries);
        // 3: Conectar los portales entre sí (Caminos internos)
        ConnectPortals(navGraph, portalGraph);
    }

    public void Clear(PortalGraph portalGraph)
    {

    }

    public void Rebake(INavGraph navGraph, PortalGraph portalGraph)
    {
        Clear(portalGraph);
        Bake(navGraph, portalGraph);
    }

    public Dictionary<(int, int), List<(int nodeA, int nodeB)>> GenerateFrontiers(INavGraph navGraph, PortalGraph portalGraph)
    {
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

    public void GeneratePortals(INavGraph navGraph, PortalGraph portalGraph, Dictionary<(int, int), List<(int nodeA, int nodeB)>> boundaries)
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

    private void ConnectPortals(INavGraph navGraph, PortalGraph portalGraph)
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

                    // Si el FloodFill usa float[] (con -1 o float.MaxValue para 'no visitado')
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

    private void AddPortalToMap(Dictionary<int, List<PortalNode>> map, int regionId, PortalNode portal)
    {
        if (!map.ContainsKey(regionId)) map[regionId] = new List<PortalNode>();
        if (!map[regionId].Contains(portal)) map[regionId].Add(portal);
    }
}