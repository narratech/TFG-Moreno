using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Este grafo representa las conexiones entre regiones a través de portales. Cada nodo es un 
/// portal que conecta dos regiones, y las aristas representan la posibilidad de moverse entre 
/// esos portales (y por ende entre las regiones). Este grafo es independiente del grafo de 
/// navegación interno de cada región, y se utiliza para planificar rutas a alto nivel entre 
/// regiones antes de entrar en detalles dentro de cada una.
/// </summary>
public class PortalGraph
{
    private List<PortalNode> _portals;
    // Clave: Id del portal, Valor: Lista de aristas hacia otros portales
    private Dictionary<int, List<PortalEdge>> _adjacency;

    private Dictionary<int, List<PortalNode>> _regionToPortals;

    public PortalGraph()
    {
        _portals = new List<PortalNode>();
        _adjacency = new Dictionary<int, List<PortalEdge>>();
        _regionToPortals = new Dictionary<int, List<PortalNode>>();
    }

    public void AddPortal(PortalNode portal)
    {
        if (_adjacency.ContainsKey(portal.Id)) return; // Evitar duplicados

        _portals.Add(portal);
        _adjacency[portal.Id] = new List<PortalEdge>();

        // Mapeamos el portal a sus regiones para facilitar búsquedas posteriores
        if (!_regionToPortals.ContainsKey(portal.RegionA))
            _regionToPortals[portal.RegionA] = new List<PortalNode>();
        if (!_regionToPortals.ContainsKey(portal.RegionB))
            _regionToPortals[portal.RegionB] = new List<PortalNode>();
        _regionToPortals[portal.RegionA].Add(portal);
        _regionToPortals[portal.RegionB].Add(portal);
    }

    /// <summary>
    /// Elimina un portal y todas sus conexiones.
    /// </summary>
    public void RemovePortal(int portalId)
    {
        if (!_adjacency.ContainsKey(portalId)) return;

        // 1. Buscamos a todos los vecinos del portal que vamos a borrar
        // para que ellos también eliminen su conexión hacia este portal.
        foreach (var edge in _adjacency[portalId])
        {
            int neighborId = edge.TargetPortalId;
            if (_adjacency.ContainsKey(neighborId)) 
            {
                // El vecino quita la arista que apunta al portal eliminado
                _adjacency[neighborId].RemoveAll(e => e.TargetPortalId == portalId);
            }
        }

        // 2. Borramos la lista de adyacencia del portal
        _adjacency.Remove(portalId);

        // 3. Lo quitamos de la lista maestra
        _portals.RemoveAll(p => p.Id == portalId);

        // 4. Lo quitamos de los mapeos de regiones
        PortalNode portal = GetPortal(portalId);
        if (portal != null)
        {
            _regionToPortals[portal.RegionA].RemoveAll(p => p.Id == portalId);
            _regionToPortals[portal.RegionB].RemoveAll(p => p.Id == portalId);
        }
    }

    public void AddEdge(int portalId1, int portalId2, float cost)
    {
        if (!_adjacency.ContainsKey(portalId1) || !_adjacency.ContainsKey(portalId2)) return;

        // Evitar duplicados antes de añadir
        RemoveEdge(portalId1, portalId2);

        // Región común entre los dos portales para asignar el costo correctamente
        int regionId = GetPortal(portalId1).RegionA == GetPortal(portalId2).RegionA || 
                       GetPortal(portalId1).RegionA == GetPortal(portalId2).RegionB ? 
                       GetPortal(portalId1).RegionA : GetPortal(portalId1).RegionB;

        _adjacency[portalId1].Add(new PortalEdge(portalId2, cost, regionId));
        _adjacency[portalId2].Add(new PortalEdge(portalId1, cost, regionId));
    }

    public void RemoveEdge(int portalId1, int portalId2)
    {
        if (_adjacency.ContainsKey(portalId1))
            _adjacency[portalId1].RemoveAll(e => e.TargetPortalId == portalId2);

        if (_adjacency.ContainsKey(portalId2))
            _adjacency[portalId2].RemoveAll(e => e.TargetPortalId == portalId1);
    }

    public void Clear()
    {
        if (_portals.Count > 0) 
            _portals.Clear();

        foreach (var edges in _adjacency.Values)
            edges.Clear();

        if (_adjacency.Count > 0) 
            _adjacency.Clear();
    }

    public IEnumerable<PortalEdge> GetNeighbors(int portalId)
    {
        return _adjacency.TryGetValue(portalId, out var edges) ? edges : new List<PortalEdge>();
    }

    public PortalNode GetPortal(int id) => _portals.Find(p => p.Id == id);

    public IEnumerable<PortalNode> GetAllPortals() => _portals;
    public int Size => _portals.Count;

    public List<int> GetPortalsBetweenRegions(int regionA, int regionB)
    {
        List<int> portalIds = new List<int>();
        if (_regionToPortals.TryGetValue(regionA, out var portalsA))
        {
            foreach (var portal in portalsA)
            {
                if ((portal.RegionA == regionB || portal.RegionB == regionB) && !portalIds.Contains(portal.Id))
                {
                    portalIds.Add(portal.Id);
                }
            }
        }
        return portalIds;
    }

    public List<PortalNode> GetPortalsInRegion(int regionId)
    {
        return _regionToPortals.TryGetValue(regionId, out var portals) ? portals : new List<PortalNode>();
    }
}

public class PortalNode
{
    public int Id;

    // Los dos nodos del INavGraph que forman el paso
    public int NodeA;
    public int NodeB;

    // Las regiones que conectan
    public int RegionA;
    public int RegionB;

    public Vector3 PositionA;
    public Vector3 PositionB;

    public PortalNode(int id, int nodeA, int nodeB, int regA, int regB, Vector3 posA, Vector3 posB)
    {
        Id = id;
        NodeA = nodeA;
        NodeB = nodeB;
        RegionA = regA;
        RegionB = regB;
        PositionA = posA;
        PositionB = posB;
    }
}

public struct PortalEdge
{
    public int TargetPortalId;
    public float Cost;
    public int RegionId;

    public PortalEdge(int id, float cost, int RegId)
    {
        TargetPortalId = id;
        Cost = cost;
        RegionId = RegId;
    }
}