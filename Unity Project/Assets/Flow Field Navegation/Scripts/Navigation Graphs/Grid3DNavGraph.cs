using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.Image;

public class Grid3DNavGraph : INavGraph
{
    // --- DIMENSIONES ---
    private readonly int _width;
    private readonly int _height;
    private readonly int _depth;
    private readonly float _cellSize;
    private readonly Vector3 _origin;

    public int Width => _width;
    public int Height => _height;
    public int Depth => _depth;
    public float CellSize => _cellSize;
    public Vector3 Origin => _origin;
    public int NodeCount => _width * _height * _depth;

    // --- REGIONES ---
    private readonly int _regW;
    private readonly int _regH;
    private readonly int _regD;

    private readonly int _regionsPerRow;
    private readonly int _regionsPerCol;
    private readonly int _regionsPerDepth;

    public int RegionWidth => _regW;
    public int RegionHeight => _regH;
    public int RegionDepth => _regD;
    public int RegionCount => _regionsPerRow * _regionsPerCol * _regionsPerDepth;

    // --- DATOS ---
    private readonly float[] _staticCosts;
    private readonly bool[] _walkability;

    public int GraphId { get; set; }

    // --- CONSTRUCTOR ---
    public Grid3DNavGraph(
        int width,
        int height,
        int depth,
        float cellSize,
        int regionWidth,
        int regionHeight,
        int regionDepth,
        Vector3 origin)
    {
        _width = width;
        _height = height;
        _depth = depth;
        _cellSize = cellSize;
        _origin = origin;

        _regW = regionWidth;
        _regH = regionHeight;
        _regD = regionDepth;

        _regionsPerRow = Mathf.CeilToInt((float)_width / _regW);
        _regionsPerCol = Mathf.CeilToInt((float)_height / _regH);
        _regionsPerDepth = Mathf.CeilToInt((float)_depth / _regD);

        int count = NodeCount;

        _staticCosts = new float[count];
        _walkability = new bool[count];

        Array.Fill(_walkability, true);

        for (int i = 0; i < count; i++)
            _staticCosts[i] = 1f;
    }

    // --- INDEXADO ---
    private int CoordToIndex(int x, int y, int z)
    {
        return z * (_width * _height) + y * _width + x;
    }

    private void IndexToCoord(int index, out int x, out int y, out int z)
    {
        x = index % _width;
        y = (index / _width) % _height;
        z = index / (_width * _height);
    }

    // --- INTERFAZ ---

    public float GetNodeCost(int index)
    {
        return _staticCosts[index];
    }

    public bool IsWalkable(int index) => _walkability[index];

    public Vector3 GetNodePosition(int index)
    {
        IndexToCoord(index, out int x, out int y, out int z);

        return _origin + new Vector3(
            x * _cellSize,
            y * _cellSize,
            z * _cellSize
        );
    }

    public Vector3 GetNodeSize(int index)
    {
        return new Vector3(_cellSize, _cellSize, _cellSize);
    }

    public int GetClosestNode(Vector3 worldPos)
    {
        Vector3 local = worldPos - _origin;

        int x = Mathf.Clamp(Mathf.RoundToInt(local.x / _cellSize), 0, _width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(local.y / _cellSize), 0, _height - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(local.z / _cellSize), 0, _depth - 1);

        return CoordToIndex(x, y, z);
    }
    public int GetLocalNode(int globalNode)
    {
        IndexToCoord(globalNode, out int x, out int y, out int z);

        int localX = x % _regW;
        int localY = y % _regH;
        int localZ = z % _regD;

        return localZ * (_regW * _regH) + localY * _regW + localX;
    }

    public int GetGlobalNode(int localNode, int regionId)
    {
        int slice = _regionsPerRow * _regionsPerCol;

        int rz = regionId / slice;
        int rem = regionId % slice;

        int ry = rem / _regionsPerRow;
        int rx = rem % _regionsPerRow;

        int localX = localNode % _regW;
        int localY = (localNode / _regW) % _regH;
        int localZ = localNode / (_regW * _regH);

        int globalX = rx * _regW + localX;
        int globalY = ry * _regH + localY;
        int globalZ = rz * _regD + localZ;

        // VALIDACIÓN CRÍTICA (bordes)
        if (globalX >= _width || globalY >= _height || globalZ >= _depth)
            return -1;

        return CoordToIndex(globalX, globalY, globalZ);
    }

    public IEnumerable<int> GetNeighbors(int index)
    {
        IndexToCoord(index, out int x, out int y, out int z);

        int[] dx = { 1, -1, 0, 0, 0, 0 };
        int[] dy = { 0, 0, 1, -1, 0, 0 };
        int[] dz = { 0, 0, 0, 0, 1, -1 };

        for (int i = 0; i < 6; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            int nz = z + dz[i];

            if (nx >= 0 && ny >= 0 && nz >= 0 &&
                nx < _width && ny < _height && nz < _depth)
            {
                int nIndex = CoordToIndex(nx, ny, nz);

                if (IsWalkable(nIndex))
                    yield return nIndex;
            }
        }
    }

    public float GetDistanceBetweenNeighbors(int from, int to)
    {
        // Con 6 vecinos siempre es movimiento en eje
        return _cellSize;
    }

    public void SetWalkable(int index, bool walkable)
    {
        _walkability[index] = walkable;
    }

    // --- REGIONES ---

    public int GetRegionId(int nodeIndex)
    {
        IndexToCoord(nodeIndex, out int x, out int y, out int z);

        int rx = x / _regW;
        int ry = y / _regH;
        int rz = z / _regD;

        return rz * (_regionsPerRow * _regionsPerCol)
             + ry * _regionsPerRow
             + rx;
    }

    public IEnumerable<int> GetNodesInRegion(int regionId)
    {
        int slice = _regionsPerRow * _regionsPerCol;

        int rz = regionId / slice;
        int rem = regionId % slice;

        int ry = rem / _regionsPerRow;
        int rx = rem % _regionsPerRow;

        int xMin = rx * _regW;
        int yMin = ry * _regH;
        int zMin = rz * _regD;

        int xMax = Mathf.Min(xMin + _regW, _width);
        int yMax = Mathf.Min(yMin + _regH, _height);
        int zMax = Mathf.Min(zMin + _regD, _depth);

        for (int z = zMin; z < zMax; z++)
        {
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    yield return CoordToIndex(x, y, z);
                }
            }
        }
    }

    public int GetRegionSize(int regionId)
    {
        int slice = _regionsPerRow * _regionsPerCol;

        int rz = regionId / slice;
        int rem = regionId % slice;

        int ry = rem / _regionsPerRow;
        int rx = rem % _regionsPerRow;

        int xMin = rx * _regW;
        int yMin = ry * _regH;
        int zMin = rz * _regD;

        int xMax = Mathf.Min(xMin + _regW, _width);
        int yMax = Mathf.Min(yMin + _regH, _height);
        int zMax = Mathf.Min(zMin + _regD, _depth);

        return (xMax - xMin) * (yMax - yMin) * (zMax - zMin);
    }

    public int GetInterpolationNodes( Vector3 worldPosition, Span<int> nodes)
    {
        Vector3 local = worldPosition - _origin;

        float gx = local.x / _cellSize;
        float gy = local.y / _cellSize;
        float gz = local.z / _cellSize;

        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);
        int z0 = Mathf.FloorToInt(gz);

        int x1 = x0 + 1;
        int y1 = y0 + 1;
        int z1 = z0 + 1;

        int count = 0;

        AddNode(x0, y0, z0, nodes, ref count);
        AddNode(x1, y0, z0, nodes, ref count);

        AddNode(x0, y1, z0, nodes, ref count);
        AddNode(x1, y1, z0, nodes, ref count);

        AddNode(x0, y0, z1, nodes, ref count);
        AddNode(x1, y0, z1, nodes, ref count);

        AddNode(x0, y1, z1, nodes, ref count);
        AddNode(x1, y1, z1, nodes, ref count);

        return count;
    }

    private void AddNode(
    int x,
    int y,
    int z,
    Span<int> nodes,
    ref int count)
    {
        if (x < 0 || x >= _width)
            return;

        if (y < 0 || y >= _height)
            return;

        if (z < 0 || z >= _depth)
            return;

        nodes[count++] = CoordToIndex(x, y, z);
    }

    public int GetNodesInRadius(int centerNode, int radius, Span<int> nodes)
    {
        IndexToCoord(centerNode, out int cx, out int cy, out int cz);

        int count = 0;

        for (int z = cz - radius; z <= cz + radius; z++)
        {
            if (z < 0 || z >= _depth)
                continue;

            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (y < 0 || y >= _height)
                    continue;

                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= _width)
                        continue;

                    if (Mathf.Abs(x - cx)
                      + Mathf.Abs(y - cy)
                      + Mathf.Abs(z - cz) > radius)
                        continue;

                    nodes[count++] = CoordToIndex(x, y, z);
                }
            }
        }

        return count;
    }

    public Vector3 GetNodeNormal(int node)
    {
        return Vector3.zero;
    }

    public void ConstrainPositionAndRotation(
    ref Vector3 position,
    ref Vector3 velocity,
    ref Quaternion rotation)
    {
        int node = GetClosestNode(position);

        if (!IsWalkable(node))
        {
            Vector3 projected = GetClosestPointOnNode(node, position);

            Vector3 normal = (position - projected).normalized;

            if (normal.sqrMagnitude > 0.0001f)
                velocity = Vector3.ProjectOnPlane(velocity, normal);

            position = projected;
        }
    }

    public Vector3 GetClosestPointOnNode(
    int node,
    Vector3 position)
    {
        IndexToCoord(node, out int x, out int y, out int z);

        float minX = _origin.x + (x - 0.5f) * _cellSize;
        float maxX = _origin.x + (x + 0.5f) * _cellSize;

        float minY = _origin.y + (y - 0.5f) * _cellSize;
        float maxY = _origin.y + (y + 0.5f) * _cellSize;

        float minZ = _origin.z + (z - 0.5f) * _cellSize;
        float maxZ = _origin.z + (z + 0.5f) * _cellSize;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);

        return position;
    }
}