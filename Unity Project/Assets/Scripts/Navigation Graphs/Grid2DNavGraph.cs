using System;
using System.Collections.Generic;
using UnityEngine;

public class Grid2DNavGraph : INavGraph
{
    // --- DATOS BÁSICOS DEL GRID ---
    private readonly int _width;
    private readonly int _height;
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    // --- DATOS DE REGIONES ---
    private readonly int _regW;
    private readonly int _regH;
    private readonly int _regionsPerRow;
    private readonly int _regionsPerCol;
    private readonly int _nodesPerRegion;

    // --- DATOS DEL COST FIELD ---
    private readonly float[] _staticCosts;
    private readonly float[] _dynamicCosts;
    private readonly bool[] _walkability;

    public int NodeCount => _width * _height;
    public int RegionCount => _regionsPerRow * _regionsPerCol;

    public event System.Action OnGraphUpdated;

    public Grid2DNavGraph(
        int width, 
        int height, 
        float cellSize, 
        int regionWidth, 
        int regionHeight, 
        Vector3 origin)
    {
        _width = width;
        _height = height;
        _cellSize = cellSize;
        _origin = origin;
        _regW = regionWidth;
        _regH = regionHeight;

        // Precalculamos el número de regiones
        _regionsPerRow = Mathf.CeilToInt((float)width / regionWidth);
        _regionsPerCol = Mathf.CeilToInt((float)height / regionHeight);
        _nodesPerRegion = regionWidth * regionHeight;

        _staticCosts = new float[NodeCount];
        _dynamicCosts = new float[NodeCount];
        _walkability = new bool[NodeCount];
        Array.Fill(_walkability, true); // Por defecto, todo es transitable

        for (int i = 0; i < NodeCount; i++) _staticCosts[i] = 1.0f;
    }

    // --- IMPLEMENTACIÓN INTERFAZ ---

    public float GetNodeCost(int index)
    {
        // El coste total es la suma de la base y lo dinámico
        return _staticCosts[index] + _dynamicCosts[index];
    }

    public bool IsWalkable(int index) => _walkability[index];

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

    public int GetLocalNode(int globalNode)
    {
        int globalX = globalNode % _width;
        int globalY = globalNode / _width;

        int localX = globalX % _regW;
        int localY = globalY % _regH;

        return localY * _regW + localX;
    }

    public int GetGlobalNode(int localNode, int regionId)
    {
        int regY = regionId / _regionsPerRow;
        int regX = regionId % _regionsPerRow;

        int localX = localNode % _regW;
        int localY = localNode / _regW;

        int globalX = (regX * _regW) + localX;
        int globalY = (regY * _regH) + localY;

        // VALIDACIÓN CRÍTICA:
        // Si la región está en el borde derecho o inferior, 
        // el nodo global podría estar fuera del mapa real.
        if (globalX >= _width || globalY >= _height)
            return -1;

        return (globalY * _width) + globalX;
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

        // Definimos las 4 direcciones: Derecha, Izquierda, Abajo, Arriba
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            // Validamos límites del mapa
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
            {
                int neighborIndex = ny * _width + nx;

                if (IsWalkable(neighborIndex))
                {
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
        _walkability[index] = walkable;
        OnGraphUpdated?.Invoke();
    }

    // --- IMPLEMENTACIÓN DE REGIONES ---
    public int GetRegionId(int nodeIndex)
    {
        int x = nodeIndex % _width;
        int y = nodeIndex / _width;

        // Dividimos la coordenada actual entre el tamaño de la región
        int regionX = x / _regW;
        int regionY = y / _regH;

        return (regionY * _regionsPerRow) + regionX;
    }

    public IEnumerable<int> GetNodesInRegion(int regionId)
    {
        int regY = regionId / _regionsPerRow;
        int regX = regionId % _regionsPerRow;

        // Inicio de la región en coordenadas de nodo
        int xMin = regX * _regW;
        int yMin = regY * _regH;

        // Final de la región (sin pasarse del borde del mundo)
        int xMax = Mathf.Min(xMin + _regW, _width);
        int yMax = Mathf.Min(yMin + _regH, _height);

        for (int y = yMin; y < yMax; y++)
        {
            for (int x = xMin; x < xMax; x++)
            {
                yield return y * _width + x;
            }
        }
    }

    public int GetRegionSize(int regionId)
    {
        int regY = regionId / _regionsPerRow;
        int regX = regionId % _regionsPerRow;
        // Inicio de la región en coordenadas de nodo
        int xMin = regX * _regW;
        int yMin = regY * _regH;
        // Final de la región (sin pasarse del borde del mundo)
        int xMax = Mathf.Min(xMin + _regW, _width);
        int yMax = Mathf.Min(yMin + _regH, _height);
        return (xMax - xMin) * (yMax - yMin);
    }

    public int GetInterpolationNodes( Vector3 worldPosition, Span<int> nodes)
    {
        Vector3 local = worldPosition - _origin;

        float gx = local.x / _cellSize;
        float gy = local.z / _cellSize;

        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);

        int x1 = x0 + 1;
        int y1 = y0 + 1;

        int count = 0;

        AddNode(x0, y0, nodes, ref count);
        AddNode(x1, y0, nodes, ref count);
        AddNode(x0, y1, nodes, ref count);
        AddNode(x1, y1, nodes, ref count);

        return count;
    }

    private void AddNode(
    int x,
    int y,
    Span<int> nodes,
    ref int count)
    {
        if (x < 0 || x >= _width)
            return;

        if (y < 0 || y >= _height)
            return;

        int index = y * _width + x;

        if (!IsWalkable(index))
            return;

        nodes[count++] = index;
    }

    public int GetNodesInRadius(int centerNode, int radius, Span<int> nodes)
    {
        int cx = centerNode % _width;
        int cy = centerNode / _width;

        int count = 0;

        for (int y = cy - radius; y <= cy + radius; y++)
        {
            if (y < 0 || y >= _height)
                continue;

            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || x >= _width)
                    continue;

                if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) > radius)
                    continue;

                nodes[count++] = y * _width + x;
            }
        }

        return count;
    }

    public Vector3 GetNodeNormal(int node)
    {
        return Vector3.up;
    }

    public void ConstrainPositionAndRotation(
    ref Vector3 position,
    ref Vector3 velocity,
    ref Quaternion rotation)
    {
    }
}