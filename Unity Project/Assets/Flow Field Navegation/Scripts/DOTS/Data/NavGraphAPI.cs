using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public static class NavGraphAPI
{
    // --- CONSULTAS Y TRANSFORMACIÓN DE ESPACIO ---

    [BurstCompile]
    public static int GetClosestNode(in NavGraphData graph, in float3 worldPos)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    float3 local = worldPos - graph.Origin;
                    int x = math.clamp((int)math.round(local.x / graph.CellSize), 0, graph.Width - 1);
                    int z = math.clamp((int)math.round(local.z / graph.CellSize), 0, graph.Height - 1);
                    return z * graph.Width + x;
                }
            case NavGraphType.Grid3D:
                {
                    float3 local = worldPos - graph.Origin;
                    int x = math.clamp((int)math.round(local.x / graph.CellSize), 0, graph.Width - 1);
                    int y = math.clamp((int)math.round(local.y / graph.CellSize), 0, graph.Height - 1);
                    int z = math.clamp((int)math.round(local.z / graph.CellSize), 0, graph.Depth - 1);
                    return z * (graph.Width * graph.Height) + y * graph.Width + x;
                }
            case NavGraphType.QuadSphere:
                {
                    float3 dir = math.normalize(worldPos - graph.Origin);
                    float3 localDir = math.rotate(math.inverse(graph.Rotation), dir);
                    CubeCoordinate coord = DirectionToCubeCoordinate(localDir, graph.Width);
                    return (int)coord.Face * (graph.Width * graph.Width) + (coord.Y * graph.Width) + coord.X;
                }
            default: return -1;
        }
    }

    [BurstCompile]
    public static float3 GetNodePosition(in NavGraphData graph, int index)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int x = index % graph.Width;
                    int y = index / graph.Width;
                    return graph.Origin + new float3(x * graph.CellSize, 0, y * graph.CellSize);
                }
            case NavGraphType.Grid3D:
                {
                    int x = index % graph.Width;
                    int y = (index / graph.Width) % graph.Height;
                    int z = index / (graph.Width * graph.Height);
                    return graph.Origin + new float3(x * graph.CellSize, y * graph.CellSize, z * graph.CellSize);
                }
            case NavGraphType.QuadSphere:
                {
                    int nodesPerFace = graph.Width * graph.Width;
                    CubeFace face = (CubeFace)(index / nodesPerFace);
                    int localIndex = index % nodesPerFace;
                    int x = localIndex % graph.Width;
                    int y = localIndex / graph.Width;

                    CubeCoordinate coord = new CubeCoordinate(face, x, y);
                    float3 dir = CubeCoordinateToDirection(coord, graph.Width);
                    return graph.Origin + math.rotate(graph.Rotation, dir) * graph.Radius;
                }
            default: return float3.zero;
        }
    }

    [BurstCompile]
    public static float3 GetNodeSize(in NavGraphData graph, int index)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                return new float3(graph.CellSize, 0, graph.CellSize);
            case NavGraphType.Grid3D:
                return new float3(graph.CellSize, graph.CellSize, graph.CellSize);
            case NavGraphType.QuadSphere:
                {
                    float size = (graph.Radius * math.PI * 2f) / (graph.Width * 4f);
                    return new float3(size, size, size);
                }
            default: return float3.zero;
        }
    }

    [BurstCompile]
    public static float3 GetNodeNormal(in NavGraphData graph, int index)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                return new float3(0, 1, 0);
            case NavGraphType.Grid3D:
                return float3.zero;
            case NavGraphType.QuadSphere:
                {
                    float3 nodePos = GetNodePosition(graph, index);
                    return math.normalize(nodePos - graph.Origin);
                }
            default: return float3.zero;
        }
    }

    [BurstCompile]
    public static float3 GetClosestPointOnNode(in NavGraphData graph, int node, in float3 position)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int x = node % graph.Width;
                    int z = node / graph.Width;

                    float minX = graph.Origin.x + (x - 0.5f) * graph.CellSize;
                    float maxX = graph.Origin.x + (x + 0.5f) * graph.CellSize;
                    float minZ = graph.Origin.z + (z - 0.5f) * graph.CellSize;
                    float maxZ = graph.Origin.z + (z + 0.5f) * graph.CellSize;

                    return new float3(
                        math.clamp(position.x, minX, maxX),
                        graph.Origin.y,
                        math.clamp(position.z, minZ, maxZ)
                    );
                }
            case NavGraphType.Grid3D:
                {
                    int x = node % graph.Width;
                    int y = (node / graph.Width) % graph.Height;
                    int z = node / (graph.Width * graph.Height);

                    float minX = graph.Origin.x + (x - 0.5f) * graph.CellSize;
                    float maxX = graph.Origin.x + (x + 0.5f) * graph.CellSize;
                    float minY = graph.Origin.y + (y - 0.5f) * graph.CellSize;
                    float maxY = graph.Origin.y + (y + 0.5f) * graph.CellSize;
                    float minZ = graph.Origin.z + (z - 0.5f) * graph.CellSize;
                    float maxZ = graph.Origin.z + (z + 0.5f) * graph.CellSize;

                    return new float3(
                        math.clamp(position.x, minX, maxX),
                        math.clamp(position.y, minY, maxY),
                        math.clamp(position.z, minZ, maxZ)
                    );
                }
            case NavGraphType.QuadSphere:
                {
                    float3 center = GetNodePosition(graph, node);
                    float3 normal = math.normalize(center - graph.Origin);
                    float distance = math.dot(position - center, normal);
                    return position - normal * distance;
                }
            default: return position;
        }
    }

    [BurstCompile]
    public static void ConstrainPositionAndRotation(
        in NavGraphData graph,
        in NativeArray<bool> walkability,
        ref float3 position,
        ref float3 velocity,
        ref quaternion rotation)
    {
        if (graph.Type == NavGraphType.QuadSphere)
        {
            float3 normal = math.normalize(position - graph.Origin);
            position = graph.Origin + normal * graph.Radius;

            int node = GetClosestNode(graph, position);
            if (!IsWalkable(graph, walkability, node))
            {
                float3 projected = GetClosestPointOnNode(graph, node, position);
                float3 correction = projected - position;

                if (math.lengthsq(correction) > 0.0001f)
                {
                    float3 wallNormal = math.normalize(correction);
                    velocity = velocity - wallNormal * math.dot(velocity, wallNormal);
                }

                position = projected;
                normal = math.normalize(position - graph.Origin);
            }

            if (math.lengthsq(velocity) > 0.0001f)
            {
                float3 forward = velocity - normal * math.dot(velocity, normal);
                if (math.lengthsq(forward) > 0.0001f)
                {
                    rotation = quaternion.LookRotation(math.normalize(forward), normal);
                }
            }
        }
        else
        {
            int node = GetClosestNode(graph, position);
            if (!IsWalkable(graph, walkability, node))
            {
                float3 projected = GetClosestPointOnNode(graph, node, position);
                float3 normal = math.normalize(position - projected);

                if (math.lengthsq(normal) > 0.0001f)
                {
                    velocity = velocity - normal * math.dot(velocity, normal);
                }

                position = projected;
            }
        }
    }

    // --- VECINOS, DISTANCIAS E INTERPOLACIÓN ---

    [BurstCompile]
    public static void GetNeighbors(
        in NavGraphData graph,
        in NativeArray<bool> walkability,
        int index,
        ref FixedList64Bytes<int> neighbors)
    {
        neighbors.Clear();

        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int x = index % graph.Width;
                    int y = index / graph.Width;
                    int4 dx = new int4(1, -1, 0, 0);
                    int4 dy = new int4(0, 0, 1, -1);

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        if (nx >= 0 && nx < graph.Width && ny >= 0 && ny < graph.Height)
                        {
                            int nIndex = ny * graph.Width + nx;
                            if (IsWalkable(graph, walkability, nIndex))
                                neighbors.Add(nIndex);
                        }
                    }
                    break;
                }
            case NavGraphType.Grid3D:
                {
                    int x = index % graph.Width;
                    int y = (index / graph.Width) % graph.Height;
                    int z = index / (graph.Width * graph.Height);

                    for (int i = 0; i < 6; i++)
                    {
                        int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                        int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                        int nz = z + (i == 4 ? 1 : i == 5 ? -1 : 0);

                        if (nx >= 0 && nx < graph.Width && ny >= 0 && ny < graph.Height && nz >= 0 && nz < graph.Depth)
                        {
                            int nIndex = nz * (graph.Width * graph.Height) + ny * graph.Width + nx;
                            if (IsWalkable(graph, walkability, nIndex))
                                neighbors.Add(nIndex);
                        }
                    }
                    break;
                }
            case NavGraphType.QuadSphere:
                {
                    int nodesPerFace = graph.Width * graph.Width;
                    CubeFace face = (CubeFace)(index / nodesPerFace);
                    int localIndex = index % nodesPerFace;
                    int x = localIndex % graph.Width;
                    int y = localIndex / graph.Width;

                    CubeCoordinate coord = new CubeCoordinate(face, x, y);

                    for (int i = 0; i < 4; i++)
                    {
                        CubeCoordinate neighborCoord = GetQuadSphereNeighbor(coord, (CubeDirection)i, graph.Width);
                        int nIndex = (int)neighborCoord.Face * nodesPerFace + (neighborCoord.Y * graph.Width) + neighborCoord.X;

                        if (IsWalkable(graph, walkability, nIndex))
                            neighbors.Add(nIndex);
                    }
                    break;
                }
        }
    }

    [BurstCompile]
    public static float GetDistanceBetweenNeighbors(in NavGraphData graph, int fromIndex, int toIndex)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int x1 = fromIndex % graph.Width;
                    int y1 = fromIndex / graph.Width;
                    int x2 = toIndex % graph.Width;
                    int y2 = toIndex / graph.Width;
                    return (x1 != x2 && y1 != y2) ? 1.41421356f * graph.CellSize : graph.CellSize;
                }
            case NavGraphType.Grid3D:
                return graph.CellSize;
            case NavGraphType.QuadSphere:
                {
                    float3 posA = GetNodePosition(graph, fromIndex);
                    float3 posB = GetNodePosition(graph, toIndex);
                    return math.distance(posA, posB);
                }
            default: return 0f;
        }
    }

    [BurstCompile]
    public static void GetInterpolationNodes(
        in NavGraphData graph,
        in NativeArray<bool> walkability,
        in float3 worldPosition,
        ref FixedList64Bytes<int> nodes)
    {
        nodes.Clear();

        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    float3 local = worldPosition - graph.Origin;
                    float gx = local.x / graph.CellSize;
                    float gy = local.z / graph.CellSize;

                    int x0 = (int)math.floor(gx);
                    int y0 = (int)math.floor(gy);

                    AddNodeIfValid2D(graph, walkability, x0, y0, ref nodes);
                    AddNodeIfValid2D(graph, walkability, x0 + 1, y0, ref nodes);
                    AddNodeIfValid2D(graph, walkability, x0, y0 + 1, ref nodes);
                    AddNodeIfValid2D(graph, walkability, x0 + 1, y0 + 1, ref nodes);
                    break;
                }
            case NavGraphType.Grid3D:
                {
                    float3 local = worldPosition - graph.Origin;
                    int x0 = (int)math.floor(local.x / graph.CellSize);
                    int y0 = (int)math.floor(local.y / graph.CellSize);
                    int z0 = (int)math.floor(local.z / graph.CellSize);

                    for (int z = z0; z <= z0 + 1; z++)
                        for (int y = y0; y <= y0 + 1; y++)
                            for (int x = x0; x <= x0 + 1; x++)
                                AddNodeIfValid3D(graph, walkability, x, y, z, ref nodes);
                    break;
                }
            case NavGraphType.QuadSphere:
                {
                    float3 dir = math.normalize(worldPosition - graph.Origin);
                    float3 localDir = math.rotate(math.inverse(graph.Rotation), dir);
                    CubeFace face = GetCubeFace(localDir);
                    float2 faceUV = DirectionToUV(face, localDir);

                    float gx = faceUV.x * graph.Width - 0.5f;
                    float gy = faceUV.y * graph.Width - 0.5f;

                    int x = (int)math.floor(gx);
                    int y = (int)math.floor(gy);

                    CubeCoordinate a = WrapCubeCoordinate(new CubeCoordinate(face, x, y), graph.Width);
                    CubeCoordinate b = GetQuadSphereNeighbor(a, CubeDirection.Right, graph.Width);
                    CubeCoordinate c = GetQuadSphereNeighbor(a, CubeDirection.Up, graph.Width);
                    CubeCoordinate d = GetQuadSphereNeighbor(c, CubeDirection.Right, graph.Width);

                    int nodesPerFace = graph.Width * graph.Width;
                    nodes.Add((int)a.Face * nodesPerFace + (a.Y * graph.Width) + a.X);
                    nodes.Add((int)b.Face * nodesPerFace + (b.Y * graph.Width) + b.X);
                    nodes.Add((int)c.Face * nodesPerFace + (c.Y * graph.Width) + c.X);
                    nodes.Add((int)d.Face * nodesPerFace + (d.Y * graph.Width) + d.X);
                    break;
                }
        }
    }

    [BurstCompile]
    public static void GetNodesInRadius(
        in NavGraphData graph,
        int centerNode,
        int radius,
        ref NativeList<int> nodes)
    {
        nodes.Clear();

        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int cx = centerNode % graph.Width;
                    int cy = centerNode / graph.Width;

                    for (int y = cy - radius; y <= cy + radius; y++)
                    {
                        if (y < 0 || y >= graph.Height) continue;
                        for (int x = cx - radius; x <= cx + radius; x++)
                        {
                            if (x < 0 || x >= graph.Width) continue;
                            if (math.abs(x - cx) + math.abs(y - cy) <= radius)
                                nodes.Add(y * graph.Width + x);
                        }
                    }
                    break;
                }
            case NavGraphType.Grid3D:
                {
                    int cx = centerNode % graph.Width;
                    int cy = (centerNode / graph.Width) % graph.Height;
                    int cz = centerNode / (graph.Width * graph.Height);

                    for (int z = cz - radius; z <= cz + radius; z++)
                    {
                        if (z < 0 || z >= graph.Depth) continue;
                        for (int y = cy - radius; y <= cy + radius; y++)
                        {
                            if (y < 0 || y >= graph.Height) continue;
                            for (int x = cx - radius; x <= cx + radius; x++)
                            {
                                if (x < 0 || x >= graph.Width) continue;
                                if (math.abs(x - cx) + math.abs(y - cy) + math.abs(z - cz) <= radius)
                                    nodes.Add(z * (graph.Width * graph.Height) + y * graph.Width + x);
                            }
                        }
                    }
                    break;
                }
            case NavGraphType.QuadSphere:
                {
                    NativeQueue<(int node, int depth)> queue = new NativeQueue<(int, int)>(Allocator.Temp);
                    NativeParallelHashSet<int> visited = new NativeParallelHashSet<int>(64, Allocator.Temp);

                    queue.Enqueue((centerNode, 0));
                    visited.Add(centerNode);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        nodes.Add(current.node);

                        if (current.depth < radius)
                        {
                            int nodesPerFace = graph.Width * graph.Width;
                            CubeFace face = (CubeFace)(current.node / nodesPerFace);
                            int localIndex = current.node % nodesPerFace;
                            CubeCoordinate coord = new CubeCoordinate(face, localIndex % graph.Width, localIndex / graph.Width);

                            for (int i = 0; i < 4; i++)
                            {
                                CubeCoordinate neighborCoord = GetQuadSphereNeighbor(coord, (CubeDirection)i, graph.Width);
                                int nIndex = (int)neighborCoord.Face * nodesPerFace + (neighborCoord.Y * graph.Width) + neighborCoord.X;

                                if (visited.Add(nIndex))
                                    queue.Enqueue((nIndex, current.depth + 1));
                            }
                        }
                    }

                    queue.Dispose();
                    visited.Dispose();
                    break;
                }
        }
    }

    // --- ESTADO Y COSTES ---

    [BurstCompile]
    public static bool IsWalkable(
        in NavGraphData graph,
        in NativeArray<bool> walkability,
        int index)
    {
        return walkability[graph.NodeOffset + index];
    }

    [BurstCompile]
    public static void SetWalkable(
        in NavGraphData graph,
        ref NativeArray<bool> walkability,
        int index,
        bool isWalkable)
    {
        walkability[graph.NodeOffset + index] = isWalkable;
    }

    [BurstCompile]
    public static float GetNodeCost(
        in NavGraphData graph,
        in NativeArray<float> staticCosts,
        in NativeArray<float> dynamicCosts,
        int index)
    {
        int offsetIndex = graph.NodeOffset + index;
        float dynamicCost = dynamicCosts.IsCreated ? dynamicCosts[offsetIndex] : 0f;
        return staticCosts[offsetIndex] + dynamicCost;
    }

    // --- REGIONES Y LOCAL/GLOBAL ---

    [BurstCompile]
    public static int GetRegionId(in NavGraphData graph, int nodeIndex)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int x = nodeIndex % graph.Width;
                    int y = nodeIndex / graph.Width;
                    int regionX = x / graph.RegionWidth;
                    int regionY = y / graph.RegionHeight;
                    return regionY * graph.RegionsPerRow + regionX;
                }
            case NavGraphType.Grid3D:
                {
                    int x = nodeIndex % graph.Width;
                    int y = (nodeIndex / graph.Width) % graph.Height;
                    int z = nodeIndex / (graph.Width * graph.Height);
                    int regionX = x / graph.RegionWidth;
                    int regionY = y / graph.RegionHeight;
                    int regionZ = z / graph.RegionDepth;
                    return regionZ * (graph.RegionsPerRow * graph.RegionsPerCol) + regionY * graph.RegionsPerRow + regionX;
                }
            case NavGraphType.QuadSphere:
                {
                    int nodesPerFace = graph.Width * graph.Width;
                    CubeFace face = (CubeFace)(nodeIndex / nodesPerFace);
                    int localIndex = nodeIndex % nodesPerFace;
                    int x = localIndex % graph.Width;
                    int y = localIndex / graph.Width;
                    int regionX = x / graph.RegionWidth;
                    int regionY = y / graph.RegionHeight;
                    int regionsPerFace = graph.RegionsPerRow * graph.RegionsPerRow;
                    return (int)face * regionsPerFace + regionY * graph.RegionsPerRow + regionX;
                }
            default: return -1;
        }
    }

    [BurstCompile]
    public static int GetLocalNode(in NavGraphData graph, int globalNode)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int globalX = globalNode % graph.Width;
                    int globalY = globalNode / graph.Width;
                    return (globalY % graph.RegionHeight) * graph.RegionWidth + (globalX % graph.RegionWidth);
                }
            case NavGraphType.Grid3D:
                {
                    int x = globalNode % graph.Width;
                    int y = (globalNode / graph.Width) % graph.Height;
                    int z = globalNode / (graph.Width * graph.Height);
                    return (z % graph.RegionDepth) * (graph.RegionWidth * graph.RegionHeight) + (y % graph.RegionHeight) * graph.RegionWidth + (x % graph.RegionWidth);
                }
            case NavGraphType.QuadSphere:
                {
                    int nodesPerFace = graph.Width * graph.Width;
                    int localIndex = globalNode % nodesPerFace;
                    int x = localIndex % graph.Width;
                    int y = localIndex / graph.Width;
                    return (y % graph.RegionHeight) * graph.RegionWidth + (x % graph.RegionWidth);
                }
            default: return -1;
        }
    }

    [BurstCompile]
    public static int GetGlobalNode(in NavGraphData graph, int localNode, int regionId)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int regY = regionId / graph.RegionsPerRow;
                    int regX = regionId % graph.RegionsPerRow;
                    int localX = localNode % graph.RegionWidth;
                    int localY = localNode / graph.RegionWidth;
                    int globalX = (regX * graph.RegionWidth) + localX;
                    int globalY = (regY * graph.RegionHeight) + localY;

                    if (globalX >= graph.Width || globalY >= graph.Height) return -1;
                    return (globalY * graph.Width) + globalX;
                }
            case NavGraphType.Grid3D:
                {
                    int slice = graph.RegionsPerRow * graph.RegionsPerCol;
                    int rz = regionId / slice;
                    int rem = regionId % slice;
                    int ry = rem / graph.RegionsPerRow;
                    int rx = rem % graph.RegionsPerRow;

                    int localX = localNode % graph.RegionWidth;
                    int localY = (localNode / graph.RegionWidth) % graph.RegionHeight;
                    int localZ = localNode / (graph.RegionWidth * graph.RegionHeight);

                    int globalX = rx * graph.RegionWidth + localX;
                    int globalY = ry * graph.RegionHeight + localY;
                    int globalZ = rz * graph.RegionDepth + localZ;

                    if (globalX >= graph.Width || globalY >= graph.Height || globalZ >= graph.Depth) return -1;
                    return globalZ * (graph.Width * graph.Height) + globalY * graph.Width + globalX;
                }
            case NavGraphType.QuadSphere:
                {
                    int regionsPerFace = graph.RegionsPerRow * graph.RegionsPerRow;
                    int face = regionId / regionsPerFace;
                    int localRegion = regionId % regionsPerFace;
                    int regionX = localRegion % graph.RegionsPerRow;
                    int regionY = localRegion / graph.RegionsPerRow;

                    int localX = localNode % graph.RegionWidth;
                    int localY = localNode / graph.RegionWidth;

                    int globalX = regionX * graph.RegionWidth + localX;
                    int globalY = regionY * graph.RegionHeight + localY;

                    int nodesPerFace = graph.Width * graph.Width;
                    return face * nodesPerFace + (globalY * graph.Width) + globalX;
                }
            default: return -1;
        }
    }

    [BurstCompile]
    public static int GetRegionSize(in NavGraphData graph, int regionId)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int regY = regionId / graph.RegionsPerRow;
                    int regX = regionId % graph.RegionsPerRow;
                    int xMin = regX * graph.RegionWidth;
                    int yMin = regY * graph.RegionHeight;
                    int xMax = math.min(xMin + graph.RegionWidth, graph.Width);
                    int yMax = math.min(yMin + graph.RegionHeight, graph.Height);
                    return (xMax - xMin) * (yMax - yMin);
                }
            case NavGraphType.Grid3D:
                {
                    int slice = graph.RegionsPerRow * graph.RegionsPerCol;
                    int rz = regionId / slice;
                    int rem = regionId % slice;
                    int ry = rem / graph.RegionsPerRow;
                    int rx = rem % graph.RegionsPerRow;

                    int xMin = rx * graph.RegionWidth;
                    int yMin = ry * graph.RegionHeight;
                    int zMin = rz * graph.RegionDepth;
                    int xMax = math.min(xMin + graph.RegionWidth, graph.Width);
                    int yMax = math.min(yMin + graph.RegionHeight, graph.Height);
                    int zMax = math.min(zMin + graph.RegionDepth, graph.Depth);
                    return (xMax - xMin) * (yMax - yMin) * (zMax - zMin);
                }
            case NavGraphType.QuadSphere:
                return graph.RegionWidth * graph.RegionHeight;
            default: return 0;
        }
    }

    [BurstCompile]
    public static void GetNodesInRegion(in NavGraphData graph, int regionId, ref NativeList<int> nodes)
    {
        nodes.Clear();
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int regY = regionId / graph.RegionsPerRow;
                    int regX = regionId % graph.RegionsPerRow;
                    int xMin = regX * graph.RegionWidth;
                    int yMin = regY * graph.RegionHeight;
                    int xMax = math.min(xMin + graph.RegionWidth, graph.Width);
                    int yMax = math.min(yMin + graph.RegionHeight, graph.Height);

                    for (int y = yMin; y < yMax; y++)
                        for (int x = xMin; x < xMax; x++)
                            nodes.Add(y * graph.Width + x);
                    break;
                }
            case NavGraphType.Grid3D:
                {
                    int slice = graph.RegionsPerRow * graph.RegionsPerCol;
                    int rz = regionId / slice;
                    int rem = regionId % slice;
                    int ry = rem / graph.RegionsPerRow;
                    int rx = rem % graph.RegionsPerRow;

                    int xMin = rx * graph.RegionWidth;
                    int yMin = ry * graph.RegionHeight;
                    int zMin = rz * graph.RegionDepth;
                    int xMax = math.min(xMin + graph.RegionWidth, graph.Width);
                    int yMax = math.min(yMin + graph.RegionHeight, graph.Height);
                    int zMax = math.min(zMin + graph.RegionDepth, graph.Depth);

                    for (int z = zMin; z < zMax; z++)
                        for (int y = yMin; y < yMax; y++)
                            for (int x = xMin; x < xMax; x++)
                                nodes.Add(z * (graph.Width * graph.Height) + y * graph.Width + x);
                    break;
                }
            case NavGraphType.QuadSphere:
                {
                    int regionsPerFace = graph.RegionsPerRow * graph.RegionsPerRow;
                    int face = regionId / regionsPerFace;
                    int localRegion = regionId % regionsPerFace;
                    int regionX = localRegion % graph.RegionsPerRow;
                    int regionY = localRegion / graph.RegionsPerRow;

                    int xMin = regionX * graph.RegionWidth;
                    int yMin = regionY * graph.RegionHeight;
                    int nodesPerFace = graph.Width * graph.Width;

                    for (int y = yMin; y < yMin + graph.RegionHeight; y++)
                        for (int x = xMin; x < xMin + graph.RegionWidth; x++)
                            nodes.Add(face * nodesPerFace + (y * graph.Width) + x);
                    break;
                }
        }
    }

    // --- AUXILIARES INTERNOS DE BURST ---

    [BurstCompile]
    private static void AddNodeIfValid2D(in NavGraphData graph, in NativeArray<bool> walkability, int x, int y, ref FixedList64Bytes<int> nodes)
    {
        if (x >= 0 && x < graph.Width && y >= 0 && y < graph.Height)
        {
            int index = y * graph.Width + x;
            if (IsWalkable(graph, walkability, index)) nodes.Add(index);
        }
    }

    [BurstCompile]
    private static void AddNodeIfValid3D(in NavGraphData graph, in NativeArray<bool> walkability, int x, int y, int z, ref FixedList64Bytes<int> nodes)
    {
        if (x >= 0 && x < graph.Width && y >= 0 && y < graph.Height && z >= 0 && z < graph.Depth)
        {
            int index = z * (graph.Width * graph.Height) + y * graph.Width + x;
            if (IsWalkable(graph, walkability, index)) nodes.Add(index);
        }
    }

    [BurstCompile]
    private static CubeFace GetCubeFace(float3 dir)
    {
        float3 absDir = math.abs(dir);
        if (absDir.x >= absDir.y && absDir.x >= absDir.z)
            return dir.x >= 0 ? CubeFace.PositiveX : CubeFace.NegativeX;
        if (absDir.y >= absDir.x && absDir.y >= absDir.z)
            return dir.y >= 0 ? CubeFace.PositiveY : CubeFace.NegativeY;
        return dir.z >= 0 ? CubeFace.PositiveZ : CubeFace.NegativeZ;
    }

    [BurstCompile]
    private static float2 DirectionToUV(CubeFace face, float3 d)
    {
        float u = 0f, v = 0f;
        float3 absD = math.abs(d);
        switch (face)
        {
            case CubeFace.PositiveX: u = (-d.z / absD.x + 1f) * 0.5f; v = (d.y / absD.x + 1f) * 0.5f; break;
            case CubeFace.NegativeX: u = (d.z / absD.x + 1f) * 0.5f; v = (d.y / absD.x + 1f) * 0.5f; break;
            case CubeFace.PositiveY: u = (d.x / absD.y + 1f) * 0.5f; v = (-d.z / absD.y + 1f) * 0.5f; break;
            case CubeFace.NegativeY: u = (d.x / absD.y + 1f) * 0.5f; v = (d.z / absD.y + 1f) * 0.5f; break;
            case CubeFace.PositiveZ: u = (d.x / absD.z + 1f) * 0.5f; v = (d.y / absD.z + 1f) * 0.5f; break;
            case CubeFace.NegativeZ: u = (-d.x / absD.z + 1f) * 0.5f; v = (d.y / absD.z + 1f) * 0.5f; break;
        }
        return new float2(u, v);
    }

    [BurstCompile]
    private static CubeCoordinate DirectionToCubeCoordinate(float3 dir, int resolution)
    {
        dir = math.normalize(dir);
        CubeFace face = GetCubeFace(dir);
        float2 uv = DirectionToUV(face, dir);
        int x = math.clamp((int)math.floor(uv.x * resolution), 0, resolution - 1);
        int y = math.clamp((int)math.floor(uv.y * resolution), 0, resolution - 1);
        return new CubeCoordinate(face, x, y);
    }

    [BurstCompile]
    private static float3 CubeCoordinateToDirection(in CubeCoordinate coord, int resolution)
    {
        float u = (coord.X + 0.5f) / resolution * 2f - 1f;
        float v = (coord.Y + 0.5f) / resolution * 2f - 1f;

        float3 p;
        switch (coord.Face)
        {
            case CubeFace.PositiveX: p = new float3(1f, v, -u); break;
            case CubeFace.NegativeX: p = new float3(-1f, v, u); break;
            case CubeFace.PositiveY: p = new float3(u, 1f, -v); break;
            case CubeFace.NegativeY: p = new float3(u, -1f, v); break;
            case CubeFace.PositiveZ: p = new float3(u, v, 1f); break;
            case CubeFace.NegativeZ: p = new float3(-u, v, -1f); break;
            default: p = float3.zero; break;
        }
        return math.normalize(p);
    }

    [BurstCompile]
    private static CubeCoordinate GetQuadSphereNeighbor(in CubeCoordinate coord, CubeDirection direction, int resolution)
    {
        int x = coord.X;
        int y = coord.Y;

        switch (direction)
        {
            case CubeDirection.Left: x--; break;
            case CubeDirection.Right: x++; break;
            case CubeDirection.Up: y++; break;
            case CubeDirection.Down: y--; break;
        }

        if (x >= 0 && x < resolution && y >= 0 && y < resolution)
            return new CubeCoordinate(coord.Face, x, y);

        FaceTransition transition = GetTransition(coord.Face, direction);

        int t = direction switch
        {
            CubeDirection.Left => coord.Y,
            CubeDirection.Right => coord.Y,
            CubeDirection.Up => coord.X,
            CubeDirection.Down => coord.X,
            _ => 0
        };

        if (transition.Flip) t = resolution - 1 - t;

        int nx = 0, ny = 0;
        switch (transition.EnterEdge)
        {
            case CubeEdge.Left: nx = 0; ny = t; break;
            case CubeEdge.Right: nx = resolution - 1; ny = t; break;
            case CubeEdge.Up: nx = t; ny = resolution - 1; break;
            case CubeEdge.Down: nx = t; ny = 0; break;
        }

        return new CubeCoordinate(transition.Face, nx, ny);
    }

    [BurstCompile]
    private static CubeCoordinate WrapCubeCoordinate(in CubeCoordinate coord, int resolution)
    {
        if (coord.X >= 0 && coord.X < resolution && coord.Y >= 0 && coord.Y < resolution)
            return coord;

        if (coord.X < 0)
            return GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, 0, coord.Y), CubeDirection.Left, resolution);
        if (coord.X >= resolution)
            return GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, resolution - 1, coord.Y), CubeDirection.Right, resolution);
        if (coord.Y < 0)
            return GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, coord.X, 0), CubeDirection.Down, resolution);

        return GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, coord.X, resolution - 1), CubeDirection.Up, resolution);
    }

    [BurstCompile]
    private static FaceTransition GetTransition(CubeFace face, CubeDirection direction)
    {
        int f = (int)face;
        int d = (int)direction;

        if (f == 0) // PositiveX
        {
            if (d == 0) return new FaceTransition(CubeFace.PositiveZ, CubeEdge.Right);
            if (d == 1) return new FaceTransition(CubeFace.NegativeZ, CubeEdge.Left);
            if (d == 2) return new FaceTransition(CubeFace.PositiveY, CubeEdge.Right);
            return new FaceTransition(CubeFace.NegativeY, CubeEdge.Right, true);
        }
        if (f == 1) // NegativeX
        {
            if (d == 0) return new FaceTransition(CubeFace.NegativeZ, CubeEdge.Right);
            if (d == 1) return new FaceTransition(CubeFace.PositiveZ, CubeEdge.Left);
            if (d == 2) return new FaceTransition(CubeFace.PositiveY, CubeEdge.Left, true);
            return new FaceTransition(CubeFace.NegativeY, CubeEdge.Left);
        }
        if (f == 2) // PositiveY
        {
            if (d == 0) return new FaceTransition(CubeFace.NegativeX, CubeEdge.Up, true);
            if (d == 1) return new FaceTransition(CubeFace.PositiveX, CubeEdge.Up);
            if (d == 2) return new FaceTransition(CubeFace.NegativeZ, CubeEdge.Up, true);
            return new FaceTransition(CubeFace.PositiveZ, CubeEdge.Up);
        }
        if (f == 3) // NegativeY
        {
            if (d == 0) return new FaceTransition(CubeFace.NegativeX, CubeEdge.Down);
            if (d == 1) return new FaceTransition(CubeFace.PositiveX, CubeEdge.Down, true);
            if (d == 2) return new FaceTransition(CubeFace.PositiveZ, CubeEdge.Down);
            return new FaceTransition(CubeFace.NegativeZ, CubeEdge.Down, true);
        }
        if (f == 4) // PositiveZ
        {
            if (d == 0) return new FaceTransition(CubeFace.NegativeX, CubeEdge.Right);
            if (d == 1) return new FaceTransition(CubeFace.PositiveX, CubeEdge.Left);
            if (d == 2) return new FaceTransition(CubeFace.PositiveY, CubeEdge.Down);
            return new FaceTransition(CubeFace.NegativeY, CubeEdge.Up);
        }
        // NegativeZ
        if (d == 0) return new FaceTransition(CubeFace.PositiveX, CubeEdge.Right);
        if (d == 1) return new FaceTransition(CubeFace.NegativeX, CubeEdge.Left);
        if (d == 2) return new FaceTransition(CubeFace.PositiveY, CubeEdge.Up, true);
        return new FaceTransition(CubeFace.NegativeY, CubeEdge.Down, true);
    }
}