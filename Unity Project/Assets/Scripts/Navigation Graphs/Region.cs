using System.Collections.Generic;
using UnityEngine;

public class Region
{
    public int RegionID { get; private set; }

    // Los nodos del INavigationGraph que pertenecen a esta región
    // Usamos un Hashset para búsquedas rápidas de O(1)
    private readonly HashSet<int> _nodes = new HashSet<int>();

    // Portales: Conexiones hacia otras regiones
    // Clave: ID de la región vecina. Valor: Lista de nodos "frontera"
    public Dictionary<int, List<PortalNode>> Gateways { get; private set; }

    public Region(int id)
    {
        RegionID = id;
        Gateways = new Dictionary<int, List<PortalNode>>();
    }

    public void AddNode(int nodeIndex) => _nodes.Add(nodeIndex);

    public bool Contains(int nodeIndex) => _nodes.Contains(nodeIndex);

    public IEnumerable<int> GetNodes() => _nodes;
}

// Estructura para definir un punto de paso entre regiones
public struct PortalNode
{
    public int localNode;  // El nodo dentro de ESTA región
    public int remoteNode; // El nodo en la región VECINA
    public float weight;   // Coste de cruzar el portal
}