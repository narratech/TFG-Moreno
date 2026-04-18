using System.Collections.Generic;
using UnityEngine;

public struct Portal
{
    public int Id;
    public Vector3 Position;

    // Los IDs de las regiones que este portal conecta (usualmente 2)
    public int RegionA;
    public int RegionB;

    // Referencia al nodo real en el Grid base (para el FlowField)
    public int BaseNodeIndex;

    public Portal(int id, Vector3 pos, int regA, int regB, int baseIdx)
    {
        Id = id;
        Position = pos;
        RegionA = regA;
        RegionB = regB;
        BaseNodeIndex = baseIdx;
    }
}

public class NavRegion
{
    public int Id;
    public List<int> PortalIds = new List<int>(); // Portales que pertenecen a esta región

    public NavRegion(int id) => Id = id;
}

public class HierarchicalNavGraph
{
    // Almacenamiento rápido por ID
    private readonly Dictionary<int, Portal> _portals = new Dictionary<int, Portal>();
    private readonly Dictionary<int, NavRegion> _regions = new Dictionary<int, NavRegion>();

    // Grafo de adyacencia: <ID_Portal, Dictionary<ID_Vecino, Coste>>
    private readonly Dictionary<int, Dictionary<int, float>> _adjacency = new Dictionary<int, Dictionary<int, float>>();

    private int _portalIdCounter = 0;

    // --- Gestión de Portales (Nodos) ---

    public int AddPortal(Vector3 position, int regA, int regB, int baseNodeIdx)
    {
        int id = _portalIdCounter++;
        Portal p = new Portal(id, position, regA, regB, baseNodeIdx);

        _portals.Add(id, p);
        _adjacency.Add(id, new Dictionary<int, float>());

        // Registrar portal en sus regiones
        GetOrCreateRegion(regA).PortalIds.Add(id);
        if (regA != regB) GetOrCreateRegion(regB).PortalIds.Add(id);

        return id;
    }

    public void RemovePortal(int id)
    {
        if (!_portals.ContainsKey(id)) return;

        // 1. Quitar de las regiones
        Portal p = _portals[id];
        _regions[p.RegionA].PortalIds.Remove(id);
        _regions[p.RegionB].PortalIds.Remove(id);

        // 2. Limpiar aristas entrantes en vecinos
        foreach (int neighborId in _adjacency[id].Keys)
        {
            _adjacency[neighborId].Remove(id);
        }

        // 3. Borrar el portal y su lista de adyacencia
        _adjacency.Remove(id);
        _portals.Remove(id);
    }

    // --- Gestión de Aristas (Conexiones) ---

    public void AddEdge(int fromId, int toId, float cost)
    {
        if (!_portals.ContainsKey(fromId) || !_portals.ContainsKey(toId)) return;

        _adjacency[fromId][toId] = cost;
        _adjacency[toId][fromId] = cost; // Bidireccional
    }

    public void RemoveEdge(int fromId, int toId)
    {
        if (_adjacency.ContainsKey(fromId)) _adjacency[fromId].Remove(toId);
        if (_adjacency.ContainsKey(toId)) _adjacency[toId].Remove(fromId);
    }

    // --- Consultas para Dijkstra ---

    public IEnumerable<int> GetNeighbors(int portalId) => _adjacency[portalId].Keys;

    public float GetCost(int fromId, int toId) => _adjacency[fromId][toId];

    public Portal GetPortal(int id) => _portals[id];

    private NavRegion GetOrCreateRegion(int id)
    {
        if (!_regions.TryGetValue(id, out var region))
        {
            region = new NavRegion(id);
            _regions.Add(id, region);
        }
        return region;
    }
}