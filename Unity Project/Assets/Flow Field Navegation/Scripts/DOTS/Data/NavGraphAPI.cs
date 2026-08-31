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
                    DirectionToCubeCoordinate(localDir, graph.Width, out CubeCoordinate coord);
                    return (int)coord.Face * (graph.Width * graph.Width) + (coord.Y * graph.Width) + coord.X;
                }
            default: return -1;
        }
    }

    [BurstCompile]
    public static void GetNodePosition(in NavGraphData graph, in int index, out float3 position)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                {
                    int x = index % graph.Width;
                    int y = index / graph.Width;
                    position = graph.Origin + new float3(x * graph.CellSize, 0, y * graph.CellSize);
                    return;
                }
            case NavGraphType.Grid3D:
                {
                    int x = index % graph.Width;
                    int y = (index / graph.Width) % graph.Height;
                    int z = index / (graph.Width * graph.Height);
                    position = graph.Origin + new float3(x * graph.CellSize, y * graph.CellSize, z * graph.CellSize);
                    return;
                }
            case NavGraphType.QuadSphere:
                {
                    int nodesPerFace = graph.Width * graph.Width;
                    CubeFace face = (CubeFace)(index / nodesPerFace);
                    int localIndex = index % nodesPerFace;
                    int x = localIndex % graph.Width;
                    int y = localIndex / graph.Width;

                    CubeCoordinate coord = new CubeCoordinate(face, x, y);
                    CubeCoordinateToDirection(coord, graph.Width, out float3 dir);
                    position = graph.Origin + math.rotate(graph.Rotation, dir) * graph.Radius;
                    return;
                }
            default: 
                position = float3.zero;
                return;
        }
    }

    [BurstCompile]
    public static void GetNodeSize(in NavGraphData graph, in int index, out float3 size)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                size = new float3(graph.CellSize, 0, graph.CellSize);
                return;
            case NavGraphType.Grid3D:
                size = new float3(graph.CellSize, graph.CellSize, graph.CellSize);
                return;
            case NavGraphType.QuadSphere:
                {
                    float sizeValue = (graph.Radius * math.PI * 2f) / (graph.Width * 4f);
                    size = new float3(sizeValue, sizeValue, sizeValue);
                    return;
                }
            default: size = float3.zero;
                return;
        }
    }

    [BurstCompile]
    public static void GetNodeNormal(in NavGraphData graph, in int index, out float3 normal)
    {
        switch (graph.Type)
        {
            case NavGraphType.Grid2D:
                normal = new float3(0, 1, 0);
                return;
            case NavGraphType.Grid3D:
                normal = float3.zero;
                return;
            case NavGraphType.QuadSphere:
                {
                    GetNodePosition(graph, index, out float3 nodePos);
                    normal = math.normalize(nodePos - graph.Origin);
                    return;
                }
            default:
                normal = float3.zero;
                return;
        }
    }

    [BurstCompile]
    public static void GetClosestPointOnNode(in NavGraphData graph, in int node, in float3 position, out float3 closestPoint)
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

                    closestPoint = new float3(
                        math.clamp(position.x, minX, maxX),
                        graph.Origin.y,
                        math.clamp(position.z, minZ, maxZ)
                    );
                    return;
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

                    closestPoint = new float3(
                        math.clamp(position.x, minX, maxX),
                        math.clamp(position.y, minY, maxY),
                        math.clamp(position.z, minZ, maxZ)
                    );
                    return;
                }
            case NavGraphType.QuadSphere:
                {
                    GetNodePosition(graph, node, out float3 center);
                    float3 normal = math.normalize(center - graph.Origin);
                    float distance = math.dot(position - center, normal);
                    closestPoint = position - normal * distance;
                    return;
                }
            default:
                closestPoint = position;
                return;
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
                GetClosestPointOnNode(graph, node, position, out float3 projected);
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
                GetClosestPointOnNode(graph, node, position, out float3 projected);
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
                        GetQuadSphereNeighbor(coord, (CubeDirection)i, graph.Width, out CubeCoordinate neighborCoord);
                        int nIndex = (int)neighborCoord.Face * nodesPerFace + (neighborCoord.Y * graph.Width) + neighborCoord.X;

                        if (IsWalkable(graph, walkability, nIndex))
                            neighbors.Add(nIndex);
                    }
                    break;
                }
        }
    }

    [BurstCompile]
    public static float GetDistanceBetweenNeighbors(in NavGraphData graph, in int fromIndex, in int toIndex)
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
                    GetNodePosition(graph, fromIndex, out float3 posA);
                    GetNodePosition(graph, toIndex, out float3 posB);
                    return math.distance(posA, posB);
                }
            default: return 0f;
        }
    }

    [BurstCompile]
    public static void GetInterpolationNodes(
        in NavGraphData graph,
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

                    // Añade las 4 esquinas si están dentro del mapa (sin importar walkability)
                    AddNodeIfInBounds2D(graph, x0, y0, ref nodes);
                    AddNodeIfInBounds2D(graph, x0 + 1, y0, ref nodes);
                    AddNodeIfInBounds2D(graph, x0, y0 + 1, ref nodes);
                    AddNodeIfInBounds2D(graph, x0 + 1, y0 + 1, ref nodes);
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
                                AddNodeIfInBounds3D(graph, x, y, z, ref nodes);
                    break;
                }
            case NavGraphType.QuadSphere:
                {
                    float3 dir = math.normalize(worldPosition - graph.Origin);
                    float3 localDir = math.rotate(math.inverse(graph.Rotation), dir);
                    GetCubeFace(localDir, out CubeFace face);
                    DirectionToUV(face, localDir, out float2 faceUV);

                    float gx = faceUV.x * graph.Width - 0.5f;
                    float gy = faceUV.y * graph.Width - 0.5f;

                    int x = (int)math.floor(gx);
                    int y = (int)math.floor(gy);

                    WrapCubeCoordinate(new CubeCoordinate(face, x, y), graph.Width, out CubeCoordinate a);
                    WrapCubeCoordinate(new CubeCoordinate(face, x + 1, y), graph.Width, out CubeCoordinate b);
                    WrapCubeCoordinate(new CubeCoordinate(face, x, y + 1), graph.Width, out CubeCoordinate c);
                    WrapCubeCoordinate(new CubeCoordinate(face, x + 1, y + 1), graph.Width, out CubeCoordinate d);

                    int nodesPerFace = graph.Width * graph.Width;
                    nodes.Add((int)a.Face * nodesPerFace + (a.Y * graph.Width) + a.X);
                    nodes.Add((int)b.Face * nodesPerFace + (b.Y * graph.Width) + b.X);
                    nodes.Add((int)c.Face * nodesPerFace + (c.Y * graph.Width) + c.X);
                    nodes.Add((int)d.Face * nodesPerFace + (d.Y * graph.Width) + d.X);
                    break;
                }
        }
    }

    // --- Métodos auxiliares ajustados (solo validan límites de rango) ---

    [BurstCompile]
    private static void AddNodeIfInBounds2D(in NavGraphData graph, int x, int y, ref FixedList64Bytes<int> nodes)
    {
        if (x >= 0 && x < graph.Width && y >= 0 && y < graph.Height)
        {
            nodes.Add(y * graph.Width + x);
        }
    }

    [BurstCompile]
    private static void AddNodeIfInBounds3D(in NavGraphData graph, int x, int y, int z, ref FixedList64Bytes<int> nodes)
    {
        if (x >= 0 && x < graph.Width && y >= 0 && y < graph.Height && z >= 0 && z < graph.Depth)
        {
            nodes.Add(z * (graph.Width * graph.Height) + y * graph.Width + x);
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
                                GetQuadSphereNeighbor(coord, (CubeDirection)i, graph.Width, out CubeCoordinate neighborCoord);
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
    private static void GetCubeFace(in float3 dir, out CubeFace face)
    {
        float3 absDir = math.abs(dir);
        if (absDir.x >= absDir.y && absDir.x >= absDir.z)
        {
            face = dir.x >= 0 ? CubeFace.PositiveX : CubeFace.NegativeX;
            return;
        }
        if (absDir.y >= absDir.x && absDir.y >= absDir.z)
        {
            face = dir.y >= 0 ? CubeFace.PositiveY : CubeFace.NegativeY;
            return;
        }
        face = dir.z >= 0 ? CubeFace.PositiveZ : CubeFace.NegativeZ;
    }

    [BurstCompile]
    private static void DirectionToUV(in CubeFace face, in float3 d, out float2 uv)
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
        uv = new float2(u, v);
        return;
    }

    [BurstCompile]
    private static void DirectionToCubeCoordinate(in float3 dir, int resolution, out CubeCoordinate coord)
    {
        float3 normalizedDir = math.normalize(dir);
        GetCubeFace(normalizedDir, out CubeFace face);
        DirectionToUV(face, normalizedDir, out float2 uv);
        int x = math.clamp((int)math.floor(uv.x * resolution), 0, resolution - 1);
        int y = math.clamp((int)math.floor(uv.y * resolution), 0, resolution - 1);
        coord = new CubeCoordinate(face, x, y);
        return;
    }

    [BurstCompile]
    private static void CubeCoordinateToDirection(in CubeCoordinate coord, int resolution, out float3 direction)
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
        direction = math.normalize(p);
    }

    [BurstCompile]
    private static void GetQuadSphereNeighbor(
        in CubeCoordinate coord, 
        in CubeDirection direction, 
        int resolution, 
        out CubeCoordinate cubeCoordinate)
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
        {
            cubeCoordinate = new CubeCoordinate(coord.Face, x, y);
            return;
        }

        GetTransition(coord.Face, direction, out FaceTransition transition);

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

        cubeCoordinate = new CubeCoordinate(transition.Face, nx, ny);
        return;
    }

    [BurstCompile]
    private static void WrapCubeCoordinate(in CubeCoordinate coord, int resolution, out CubeCoordinate wrappedCoord)
    {
        if (coord.X >= 0 && coord.X < resolution && coord.Y >= 0 && coord.Y < resolution)
        {
            wrappedCoord = coord;
            return;
        }

        if (coord.X < 0)
        {
            GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, 0, coord.Y), CubeDirection.Left, resolution, out wrappedCoord);
            return;
        }
        if (coord.X >= resolution)
        {
            GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, resolution - 1, coord.Y), CubeDirection.Right, resolution, out wrappedCoord);
            return;
        }
        if (coord.Y < 0)
        {
            GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, coord.X, 0), CubeDirection.Down, resolution, out wrappedCoord);
            return;
        }

        GetQuadSphereNeighbor(new CubeCoordinate(coord.Face, coord.X, resolution - 1), CubeDirection.Up, resolution, out wrappedCoord);
        return;
    }


    [BurstCompile]
    private static void GetTransition(in CubeFace face, in CubeDirection direction, out FaceTransition transition)
    {
        int f = (int)face;
        int d = (int)direction;

        if (f == 0) // PositiveX
        {
            if (d == 0) { transition = new FaceTransition(CubeFace.PositiveZ, CubeEdge.Right); return; }
            if (d == 1) { transition = new FaceTransition(CubeFace.NegativeZ, CubeEdge.Left); return; }
            if (d == 2) { transition = new FaceTransition(CubeFace.PositiveY, CubeEdge.Right); return; }
            transition = new FaceTransition(CubeFace.NegativeY, CubeEdge.Right, true); return; 
        }
        if (f == 1) // NegativeX
        {
            if (d == 0) { transition = new FaceTransition(CubeFace.NegativeZ, CubeEdge.Right); return; }
            if (d == 1) { transition = new FaceTransition(CubeFace.PositiveZ, CubeEdge.Left); return; }
            if (d == 2) { transition = new FaceTransition(CubeFace.PositiveY, CubeEdge.Left, true); return; }
            transition = new FaceTransition(CubeFace.NegativeY, CubeEdge.Left); return;
        }
        if (f == 2) // PositiveY
        {
            if (d == 0) { transition = new FaceTransition(CubeFace.NegativeX, CubeEdge.Up, true); return; }
            if (d == 1) { transition = new FaceTransition(CubeFace.PositiveX, CubeEdge.Up); return; }
            if (d == 2) { transition = new FaceTransition(CubeFace.NegativeZ, CubeEdge.Up, true); return; }
            transition = new FaceTransition(CubeFace.PositiveZ, CubeEdge.Up); return;
        }
        if (f == 3) // NegativeY
        {
            if (d == 0) { transition = new FaceTransition(CubeFace.NegativeX, CubeEdge.Down); return; }
            if (d == 1) { transition = new FaceTransition(CubeFace.PositiveX, CubeEdge.Down, true); return; }
            if (d == 2) { transition = new FaceTransition(CubeFace.PositiveZ, CubeEdge.Down); return; }
            transition = new FaceTransition(CubeFace.NegativeZ, CubeEdge.Down, true); return;
        }
        if (f == 4) // PositiveZ
        {
            if (d == 0) { transition = new FaceTransition(CubeFace.NegativeX, CubeEdge.Right); return; }
            if (d == 1) { transition = new FaceTransition(CubeFace.PositiveX, CubeEdge.Left); return; }
            if (d == 2) { transition = new FaceTransition(CubeFace.PositiveY, CubeEdge.Down); return; }
            transition = new FaceTransition(CubeFace.NegativeY, CubeEdge.Up); return;
        }
        // NegativeZ
        if (d == 0) { transition = new FaceTransition(CubeFace.PositiveX, CubeEdge.Right); return; }
        if (d == 1) { transition = new FaceTransition(CubeFace.NegativeX, CubeEdge.Left); return; }
        if (d == 2) { transition = new FaceTransition(CubeFace.PositiveY, CubeEdge.Up, true); return; }
        transition = new FaceTransition(CubeFace.NegativeY, CubeEdge.Down, true); return;
    }
}