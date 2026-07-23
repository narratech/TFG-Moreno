using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa un grafo de navegación esférico basado en la proyección de un cubo (Quad-Sphere).
/// Divide la superficie de una esfera en 6 caras planas que se subdividen en una cuadrícula de resolución configurable.
/// Funciona mapeando posiciones tridimensionales en la esfera a coordenadas bidimensionales de las caras del cubo (y viceversa),
/// permitiendo calcular rutas sobre superficies esféricas, gestionar la transitabilidad de los nodos, agruparlos en regiones optimizadas
/// y conectar vecinos de forma fluida incluso a través de los bordes de las caras.
/// </summary>
public class QuadSphereNavGraph : INavGraph
{
    private readonly Vector3 _center;
    private readonly Quaternion _rotation;
    private readonly float _radius;
    private readonly int _resolution;
    private readonly int _nodesPerFace;
    private readonly Vector3[] _positions;
    private readonly bool[] _walkable;
    private readonly float[] _staticCosts;
    private readonly float[] _dynamicCosts;
    private readonly int _regionsPerAxis;
    private readonly int _regionSize;
    private readonly int _regionsPerFace;

    public Vector3 Center => _center;
    public Quaternion Rotation => _rotation;
    public float Radius => _radius;
    public int Resolution => _resolution;
    public int NodeCount => _nodesPerFace * 6;
    public int RegionCount => _regionsPerFace * 6;

    public event System.Action OnGraphUpdated;

    public QuadSphereNavGraph(
        Vector3 center,
        float radius,
        Quaternion rotation,
        int resolution,
        int regionsPerAxis)
    {
        _center = center;
        _radius = radius;
        _rotation = rotation;
        _resolution = resolution;
        _nodesPerFace = resolution * resolution;
        _regionsPerFace = regionsPerAxis * regionsPerAxis;

        _positions = new Vector3[NodeCount];
        _walkable = new bool[NodeCount];
        _staticCosts = new float[NodeCount];
        _dynamicCosts = new float[NodeCount];

        System.Array.Fill(_walkable, true);

        for (int i = 0; i < NodeCount; i++)
            _staticCosts[i] = 1f;

        _regionsPerAxis = regionsPerAxis;

        if (_resolution % _regionsPerAxis != 0)
            throw new ArgumentException("Resolution must be divisible by RegionsPerAxis.");

        _regionSize = _resolution / _regionsPerAxis;

        BuildPositions();
    }

    private void BuildPositions()
    {
        foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
        {
            for (int y = 0; y < _resolution; y++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    CubeCoordinate coord = new CubeCoordinate(face, x, y);
                    Vector3 direction = CubeProjection.CubeCoordinateToDirection(coord, _resolution);
                    int index = CoordinateToIndex(coord);

                    _positions[index] = CubeProjection.DirectionToWorld(
                        _center,
                        _radius,
                        _rotation,
                        direction);
                }
            }
        }
    }

    public CubeCoordinate IndexToCoordinate(int index)
    {
        int faceIndex = index / _nodesPerFace;
        int localIndex = index % _nodesPerFace;
        int x = localIndex % _resolution;
        int y = localIndex / _resolution;

        return new CubeCoordinate((CubeFace)faceIndex, x, y);
    }

    public int CoordinateToIndex(CubeCoordinate coord)
    {
        return ((int)coord.Face * _nodesPerFace) + (coord.Y * _resolution) + coord.X;
    }

    public Vector3 GetNodePosition(int index)
    {
        return _positions[index];
    }

    public Vector3 GetNodeSize(int index)
    {
        float size = (_radius * Mathf.PI * 2f) / (_resolution * 4f);
        return Vector3.one * size;
    }

    public int GetClosestNode(Vector3 worldPosition)
    {
        Vector3 dir = CubeProjection.WorldToDirection(_center, _rotation, worldPosition);
        CubeCoordinate coord = CubeProjection.DirectionToCubeCoordinate(dir, _resolution);
        return CoordinateToIndex(coord);
    }

    public float GetNodeCost(int index)
    {
        return _staticCosts[index] + _dynamicCosts[index];
    }

    public bool IsWalkable(int index)
    {
        return _walkable[index];
    }

    public void SetWalkable(int index, bool walkable)
    {
        _walkable[index] = walkable;
        OnGraphUpdated?.Invoke();
    }

    public float GetDistanceBetweenNeighbors(int from, int to)
    {
        return Vector3.Distance(_positions[from], _positions[to]);
    }

    public int GetRegionId(int nodeIndex)
    {
        CubeCoordinate coord = IndexToCoordinate(nodeIndex);
        int regionX = coord.X / _regionSize;
        int regionY = coord.Y / _regionSize;
        int localRegion = regionY * _regionsPerAxis + regionX;

        return ((int)coord.Face * _regionsPerFace) + localRegion;
    }

    public int GetRegionSize(int regionId)
    {
        return _regionSize * _regionSize;
    }

    public IEnumerable<int> GetNodesInRegion(int regionId)
    {
        int face = regionId / _regionsPerFace;
        int localRegion = regionId % _regionsPerFace;

        int regionX = localRegion % _regionsPerAxis;
        int regionY = localRegion / _regionsPerAxis;

        int xMin = regionX * _regionSize;
        int yMin = regionY * _regionSize;

        for (int y = yMin; y < yMin + _regionSize; y++)
        {
            for (int x = xMin; x < xMin + _regionSize; x++)
            {
                yield return CoordinateToIndex(
                    new CubeCoordinate((CubeFace)face, x, y));
            }
        }
    }

    public int GetLocalNode(int globalNode)
    {
        CubeCoordinate coord = IndexToCoordinate(globalNode);
        int localX = coord.X % _regionSize;
        int localY = coord.Y % _regionSize;

        return localY * _regionSize + localX;
    }

    public int GetGlobalNode(int localNode, int regionId)
    {
        int face = regionId / _regionsPerFace;
        int localRegion = regionId % _regionsPerFace;

        int regionX = localRegion % _regionsPerAxis;
        int regionY = localRegion / _regionsPerAxis;

        int localX = localNode % _regionSize;
        int localY = localNode / _regionSize;

        int globalX = regionX * _regionSize + localX;
        int globalY = regionY * _regionSize + localY;

        return CoordinateToIndex(
            new CubeCoordinate((CubeFace)face, globalX, globalY));
    }

    public IEnumerable<int> GetNeighbors(int index)
    {
        CubeCoordinate coord = IndexToCoordinate(index);

        CubeDirection[] directions =
        {
            CubeDirection.Left,
            CubeDirection.Right,
            CubeDirection.Up,
            CubeDirection.Down
        };

        foreach (CubeDirection direction in directions)
        {
            CubeCoordinate neighbor = CubeTopology.GetNeighbor(coord, direction, _resolution);
            int neighborIndex = CoordinateToIndex(neighbor);

            if (IsWalkable(neighborIndex))
                yield return neighborIndex;
        }
    }

    public void GetNodeCorners(
    int node,
    out Vector3 a,
    out Vector3 b,
    out Vector3 c,
    out Vector3 d)
    {
        CubeCoordinate coord = IndexToCoordinate(node);

        float step = 1f / _resolution;

        float u0 = coord.X * step;
        float u1 = (coord.X + 1) * step;

        float v0 = coord.Y * step;
        float v1 = (coord.Y + 1) * step;

        a = CubeProjection.DirectionToWorld(
            _center,
            _radius,
            _rotation,
            CubeProjection.UVToDirection(coord.Face, u0, v0));

        b = CubeProjection.DirectionToWorld(
            _center,
            _radius,
            _rotation,
            CubeProjection.UVToDirection(coord.Face, u1, v0));

        c = CubeProjection.DirectionToWorld(
            _center,
            _radius,
            _rotation,
            CubeProjection.UVToDirection(coord.Face, u1, v1));

        d = CubeProjection.DirectionToWorld(
            _center,
            _radius,
            _rotation,
            CubeProjection.UVToDirection(coord.Face, u0, v1));
    }

    public int GetInterpolationNodes(
    Vector3 worldPosition,
    Span<int> nodes)
    {
        Vector3 dir = CubeProjection.WorldToDirection(
            _center,
            _rotation,
            worldPosition);

        CubeFace face = CubeProjection.GetFace(dir);

        Vector2 faceUV = CubeProjection.DirectionToUV(face, dir);

        float gx = faceUV.x * _resolution - 0.5f;
        float gy = faceUV.y * _resolution - 0.5f;

        int x = Mathf.FloorToInt(gx);
        int y = Mathf.FloorToInt(gy);

        CubeCoordinate a = CubeTopology.WrapCoordinate(
            new CubeCoordinate(face, x, y),
            _resolution);

        CubeCoordinate b = CubeTopology.GetNeighbor(
            a,
            CubeDirection.Right,
            _resolution);

        CubeCoordinate c = CubeTopology.GetNeighbor(
            a,
            CubeDirection.Up,
            _resolution);

        CubeCoordinate d = CubeTopology.GetNeighbor(
            c,
            CubeDirection.Right,
            _resolution);

        nodes[0] = CoordinateToIndex(a);
        nodes[1] = CoordinateToIndex(b);
        nodes[2] = CoordinateToIndex(c);
        nodes[3] = CoordinateToIndex(d);

        return 4;
    }

    private readonly Queue<(int node, int depth)> _queue = new();
    private readonly HashSet<int> _visited = new();

    public int GetNodesInRadius(
        int centerNode,
        int radius,
        Span<int> nodes)
    {
        _queue.Clear();
        _visited.Clear();

        _queue.Enqueue((centerNode, 0));
        _visited.Add(centerNode);

        int count = 0;

        while (_queue.Count > 0)
        {
            var current = _queue.Dequeue();

            nodes[count++] = current.node;

            if (current.depth == radius)
                continue;

            foreach (int neighbor in GetNeighbors(current.node))
            {
                if (_visited.Add(neighbor))
                {
                    _queue.Enqueue((
                        neighbor,
                        current.depth + 1));
                }
            }
        }

        return count;
    }
}