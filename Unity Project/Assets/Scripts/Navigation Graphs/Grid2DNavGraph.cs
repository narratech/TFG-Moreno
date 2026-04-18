using System.Collections.Generic;
using UnityEngine;

public class Grid2DNavGraph : INavGraph
{
    private readonly int _width;
    private readonly int _height;
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    private readonly int _regionSize;
    private readonly int _regionsPerRow;
    private readonly int _regionsPerCol;

    // --- DATOS DEL COST FIELD ---
    private readonly float[] _staticCosts;      // Coste del terreno (hierba, agua...)
    private readonly float[] _dynamicCosts;   // Costes temporales (fuego, influencia...)
    private readonly bool[] _unwalkableNodes; // Obstáculos estáticos (muros)

    public int NodeCount => _width * _height;
    public event System.Action OnGraphUpdated;

    public Grid2DNavGraph(int width, int height, float cellSize, Vector3 origin, int regionSize)
    {
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _origin = origin;

        // Precalculamos el número de regiones
        _regionsPerRow = Mathf.CeilToInt((float)_width / _regionSize);
        _regionsPerCol = Mathf.CeilToInt((float)_height / _regionSize);

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
    public float MoveCostStraight => _cellSize;
    public float MoveCostDiagonal => 1.41421356f * _cellSize;

    public float GetDistanceBetweenNeighbors(int from, int to)
    {
        // Al ser un grid, podemos saber si es diagonal comparando 
        // si ambos ejes cambian a la vez.
        int x1 = from % _width;
        int y1 = from / _width;
        int x2 = to % _width;
        int y2 = to / _width;

        // Si la diferencia en X y en Y es distinta de cero, es diagonal
        return (x1 != x2 && y1 != y2) ? MoveCostDiagonal : MoveCostStraight;
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

    public void SetWalkable(int index, bool walkable)
    {
        _unwalkableNodes[index] = !walkable;
        OnGraphUpdated?.Invoke();
    }

    // --- IMPLEMENTACIÓN DE REGIONES ---
    public int GetRegionId(int nodeIndex)
    {
        int x = nodeIndex % _width;
        int y = nodeIndex / _width;

        // Uso de división entera para agrupar en bloques
        return (y / _regionSize) * _regionsPerRow + (x / _regionSize);
    }

    public IEnumerable<int> GetNodesInRegion(int regionId)
    {
        int regY = regionId / _regionsPerRow;
        int regX = regionId % _regionsPerRow;

        int xMin = regX * _regionSize;
        int yMin = regY * _regionSize;

        // Limitar para no salir del ancho/alto real del grid
        int xMax = Mathf.Min(xMin + _regionSize, _width);
        int yMax = Mathf.Min(yMin + _regionSize, _height);

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                yield return y * _width + x;
            }
        }
    }
}