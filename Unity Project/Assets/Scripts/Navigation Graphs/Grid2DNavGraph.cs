using System.Collections.Generic;
using UnityEngine;

public class Grid2DNavGraph : INavigationGraph
{
    private readonly int _width;
    private readonly int _height;
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    // --- DATOS DEL COST FIELD ---
    private readonly float[] _staticCosts;      // Coste del terreno (hierba, agua...)
    private readonly float[] _dynamicCosts;   // Costes temporales (fuego, influencia...)
    private readonly bool[] _unwalkableNodes; // Obstáculos estáticos (muros)

    public int NodeCount => _width * _height;
    public event System.Action OnGraphUpdated;

    public Grid2DNavGraph(int width, int height, float cellSize, Vector3 origin)
    {
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _origin = origin;

        _staticCosts = new float[NodeCount];
        _dynamicCosts = new float[NodeCount];
        _unwalkableNodes = new bool[NodeCount];

        for (int i = 0; i < NodeCount; i++) _staticCosts[i] = 1.0f;
    }

    // --- IMPLEMENTACIÓN INTERFAZ ---

    public float GetNodeCost(int index)
    {
        // El coste total es la suma de la base y lo dinámico
        return _staticCosts[index] + _dynamicCosts[index];
    }

    public bool IsWalkable(int index) => !_unwalkableNodes[index];

    public Vector3 GetNodePosition(int index)
    {
        int x = index % _width;
        int y = index / _width;
        return _origin + new Vector3(x * _cellSize, 0, y * _cellSize);
    }

    public Vector3 GetNodeSize(int index)
    {
        return new Vector3(_cellSize, 0, _cellSize); // Asumimos que todas las celdas son del mismo tamaño
    }

    public int GetClosestNode(Vector3 worldPos)
    {
        Vector3 local = worldPos - _origin;
        int x = Mathf.Clamp(Mathf.RoundToInt(local.x / _cellSize), 0, _width - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(local.z / _cellSize), 0, _height - 1);
        return z * _width + x;
    }

    // Constantes de distancias de vecinos (Optimizan legibilidad y rendimiento)
    private const float MOVE_COST_STRAIGHT = 1.0f;
    private const float MOVE_COST_DIAGONAL = 1.41421356f;

    public float GetDistanceBetweenNeighbors(int from, int to)
    {
        // Al ser un grid, podemos saber si es diagonal comparando 
        // si ambos ejes cambian a la vez.
        int x1 = from % _width;
        int y1 = from / _width;
        int x2 = to % _width;
        int y2 = to / _width;

        // Si la diferencia en X y en Y es distinta de cero, es diagonal
        return (x1 != x2 && y1 != y2) ? MOVE_COST_DIAGONAL : MOVE_COST_STRAIGHT;
    }

    public IEnumerable<int> GetNeighbors(int index)
    {
        int x = index % _width;
        int y = index / _width;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
                {
                    int neighborIndex = ny * _width + nx;
                    if (IsWalkable(neighborIndex))
                        yield return neighborIndex;
                }
            }
        }
    }

    // --- MÉTODOS DINÁMICOS ---

    public void UpdateDynamicCost(int index, float extraCost)
    {
        _dynamicCosts[index] = extraCost;
        OnGraphUpdated?.Invoke(); // Notifica que el mundo cambió
    }

    public void SetStaticObstacle(int index, bool isObstacle)
    {
        _unwalkableNodes[index] = isObstacle;
        OnGraphUpdated?.Invoke();
    }
}