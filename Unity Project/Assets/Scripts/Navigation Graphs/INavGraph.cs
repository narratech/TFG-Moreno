using System.Collections.Generic;
using UnityEngine;

public interface INavGraph
{
    int NodeCount { get; }
    int RegionCount { get; }

    // --- Transformación de Espacio y Consultas ---
    Vector3 GetNodePosition(int index);
    Vector3 GetNodeSize(int index);
    int GetClosestNode(Vector3 worldPosition);
    int GetLocalNode(int globalNode);
    int GetGlobalNode(int localNode, int regionId);

    // --- Gestión de Regiones (Clustering) ---
    /// <summary>
    /// Devuelve el identificador de la región geográfica/lógica a la que pertenece el nodo.
    /// </summary>
    int GetRegionId(int nodeIndex);

    /// <summary>
    /// Devuelve todos los nodos que pertenecen a una región específica.
    /// </summary>
    IEnumerable<int> GetNodesInRegion(int regionId);

    int GetRegionSize(int regionId);

    // --- Relaciones entre Nodos ---
    IEnumerable<int> GetNeighbors(int index);

    // --- Datos de Coste y Estado ---
    float GetNodeCost(int index);
    bool IsWalkable(int index);

    /// <summary>
    /// Distancia real entre dos nodos vecinos.
    /// </summary>
    float GetDistanceBetweenNeighbors(int fromIndex, int toIndex);

    // --- Gestión de Estado Dinámico ---
    /// <summary>
    /// Permite actualizar la transitabilidad de un nodo en tiempo de ejecución.
    /// </summary>
    void SetWalkable(int index, bool walkable);

    // Evento para notificar cambios estructurales
    event System.Action OnGraphUpdated;
}