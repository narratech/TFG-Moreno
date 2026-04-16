using System;
using System.Collections.Generic;
using UnityEngine;

public class AbstractNode
{
    public int Id;
    public int RegionID; // A qué cuadrado/cubo pertenece
    public Vector3 Center;

    // Referencia a los nodos de bajo nivel (ej: índices del Grid2D)
    public HashSet<int> LowLevelNodes;

    // Conexiones de alto nivel (IDs de otros AbstractNodes)
    public List<int> ConnectedAbstractNodes;

    // Diccionario de distancias internas: 
    // "Para ir del Portal A al Portal B dentro de esta zona, cuesta X"
    public Dictionary<int, float> IntraEdgeWeights;
}

public class HierarchicalNavGraph : INavigationGraph
{
    // Los "nodos" de este grafo son zonas abstractas, no celdas
    private List<AbstractNode> _abstractNodes = new List<AbstractNode>();

    public event Action OnGraphUpdated;

    // --- Implementación de INavigationGraph ---

    public int NodeCount => _abstractNodes.Count;

    public IEnumerable<int> GetNeighbors(int index)
    {
        // Los vecinos son otros AbstractNodes conectados por portales
        return _abstractNodes[index].ConnectedAbstractNodes;
    }

    public float GetNodeCost(int index) => 1.0f; // Coste base de la zona

    public float GetDistanceBetweenNeighbors(int from, int to)
    {
        // Distancia precalculada entre los centros de las zonas
        return Vector3.Distance(_abstractNodes[from].Center, _abstractNodes[to].Center);
    }

    public Vector3 GetNodePosition(int index) => _abstractNodes[index].Center;

    public Vector3 GetNodeSize(int index)
    {
        throw new NotImplementedException();
    }

    public bool IsWalkable(int index) => true;

    public int GetClosestNode(Vector3 worldPos)
    {
        // Lógica para encontrar qué zona abstracta contiene esta posición
        return 0;
    }
}