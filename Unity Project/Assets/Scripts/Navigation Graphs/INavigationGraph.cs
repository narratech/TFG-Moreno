using System.Collections.Generic;
using UnityEngine;

public interface INavigationGraph
{
    int NodeCount { get; }

    // Transformación de espacio
    Vector3 GetNodePosition(int index);
    Vector3 GetNodeSize(int index);
    int GetClosestNode(Vector3 worldPosition);

    // Relaciones entre nodos
    IEnumerable<int> GetNeighbors(int index);

    // Datos de coste
    float GetNodeCost(int index);
    bool IsWalkable(int index);

    // Distancia entre nodos vecinos (para heurística, Dijkstra/A*)
    float GetDistanceBetweenNeighbors(int fromIndex, int toIndex);

    // Evento para avisar a los FlowFields que deben recalcularse
    event System.Action OnGraphUpdated;
}