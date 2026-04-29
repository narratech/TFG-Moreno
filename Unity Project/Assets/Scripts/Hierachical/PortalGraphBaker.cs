using System.Collections.Generic;
using UnityEngine;

public static class PortalGraphBaker
{
    public class BoundarySegment
    {
        public int RegionA;
        public int RegionB;
        public List<(int nodeA, int nodeB)> Contacts = new List<(int, int)>();

        public BoundarySegment(int regA, int regB)
        {
            RegionA = regA;
            RegionB = regB;
        }
    }

    static public void Bake(INavGraph navGraph, PortalGraph portalGraph)
    {
        // 1: Generar las fronteras entre regiones
        var boundaries = GenerateSegments(navGraph);
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

    static private List<BoundarySegment> GenerateSegments(INavGraph navGraph)
    {
        List<BoundarySegment> allSegments = new List<BoundarySegment>();
        HashSet<(int, int)> processedEdges = new HashSet<(int, int)>();

        for (int i = 0; i < navGraph.NodeCount; i++)
        {
            if (!navGraph.IsWalkable(i)) continue;
            int regA = navGraph.GetRegionId(i);

            foreach (int neighbor in navGraph.GetNeighbors(i))
            {
                if (!navGraph.IsWalkable(neighbor)) continue;

                int regB = navGraph.GetRegionId(neighbor);

                // Solo nos interesan conexiones entre regiones distintas y válidas
                if (regA < 0 || regB < 0 || regA == regB) continue;

                // Evitar procesar A->B y luego B->A (Nodos dobles)
                var edgeKey = i < neighbor ? (i, neighbor) : (neighbor, i);
                if (processedEdges.Contains(edgeKey)) continue;
                processedEdges.Add(edgeKey);

                // Intentar añadir este par a un segmento existente que esté cerca
                bool addedToExisting = false;
                foreach (var segment in allSegments)
                {
                    // Si el segmento es para las mismas dos regiones
                    if ((segment.RegionA == regA && segment.RegionB == regB) ||
                        (segment.RegionA == regB && segment.RegionB == regA))
                    {
                        if (IsNodeAdjacentToSegment(navGraph, i, neighbor, segment))
                        {
                            segment.Contacts.Add((i, neighbor));
                            addedToExisting = true;
                            break;
                        }
                    }
                }

                // Si no está cerca de ningún segmento actual, crear uno nuevo
                if (!addedToExisting)
                {
                    var newSegment = new BoundarySegment(regA, regB);
                    newSegment.Contacts.Add((i, neighbor));
                    allSegments.Add(newSegment);
                }
            }
        }
        return allSegments;
    }

    static private bool IsNodeAdjacentToSegment(INavGraph navGraph, int nodeA, int nodeB, BoundarySegment segment)
    {
        // Un par de nodos pertenece al mismo segmento si alguno de ellos es vecino
        // de un nodo que ya está en la lista de ese segmento.
        foreach (var contact in segment.Contacts)
        {
            if (AreNodesNeighbors(navGraph, nodeA, contact.nodeA) ||
                AreNodesNeighbors(navGraph, nodeB, contact.nodeB))
            {
                return true;
            }
        }
        return false;
    }

    static private bool AreNodesNeighbors(INavGraph navGraph, int a, int b)
    {
        foreach (int n in navGraph.GetNeighbors(a)) if (n == b) return true;
        return false;
    }

    static private void GeneratePortals(INavGraph navGraph, PortalGraph portalGraph, List<BoundarySegment> segments)
    {
        foreach (var segment in segments)
        {
            // Tomamos el punto medio de la lista de contactos del segmento
            var middlePair = segment.Contacts[segment.Contacts.Count / 2];

            PortalNode newPortal = new PortalNode(
                portalGraph.Size,
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

    public static BoundarySegment GetBoundaryForPortal(INavGraph navGraph, PortalGraph portalGraph, int portalId)
    {
        PortalNode portal = portalGraph.GetPortal(portalId);
        // Generar un segmento en base a los nodos de contacto del portal
        int regA = portal.RegionA;
        int regB = portal.RegionB;

        BoundarySegment segment = new BoundarySegment(regA, regB);
        // Buscamos hacia los lados todos los nodos de la region A que estén conectados a la región B
        // cola de nodos a procesar
        Queue<int> toProcess = new Queue<int>();
        toProcess.Enqueue(portal.NodeA);
        HashSet<int> visited = new HashSet<int>();
        while (toProcess.Count > 0)
        {
            int current = toProcess.Dequeue();
            visited.Add(current);
            foreach (int neighbor in navGraph.GetNeighbors(current))
            {
                if (!navGraph.IsWalkable(neighbor)) continue;
                int neighborRegion = navGraph.GetRegionId(neighbor);
                if (neighborRegion == regB)
                {
                    // Este nodo es un contacto entre regA y regB
                    segment.Contacts.Add((current, neighbor));
                }
                else if (neighborRegion == regA && !visited.Contains(neighbor))
                {
                    // Este nodo es parte de la región A, seguir expandiendo
                    toProcess.Enqueue(neighbor);
                }
            }
        }
        return segment;
    }
}