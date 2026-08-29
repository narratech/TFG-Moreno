using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public static class NavGraphAPI
{
    // --- CONSULTAS DE NODOS ---

    [BurstCompile]
    public static int GetClosestNode(in NavGraphData graph, float3 worldPos)
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

    // --- VECINOS Y NAVEGACIÓN ---

    [BurstCompile]
    public static void GetNeighbors(
        in NavGraphData graph,
        NativeArray<bool> walkability,
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

                            if (walkability[graph.NodeOffset + nIndex])
                            {
                                neighbors.Add(nIndex);
                            }
                        }
                    }
                    break;
                }
            case NavGraphType.Grid3D:
                {
                    int x = index % graph.Width;
                    int y = (index / graph.Width) % graph.Height;
                    int z = index / (graph.Width * graph.Height);

                    // Reemplazado array managed por cálculo directo o vectores SIMD para cero alocaciones
                    int3 ox = new int3(1, -1, 0);
                    int3 oy = new int3(0, 0, 1);
                    int3 oz = new int3(0, 0, -1);

                    for (int i = 0; i < 6; i++)
                    {
                        int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                        int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                        int nz = z + (i == 4 ? 1 : i == 5 ? -1 : 0);

                        if (nx >= 0 && nx < graph.Width && ny >= 0 && ny < graph.Height && nz >= 0 && nz < graph.Depth)
                        {
                            int nIndex = nz * (graph.Width * graph.Height) + ny * graph.Width + nx;
                            if (walkability[graph.NodeOffset + nIndex])
                            {
                                neighbors.Add(nIndex);
                            }
                        }
                    }
                    break;
                }
        }
    }

    // --- ESTADO Y COSTES ---

    [BurstCompile]
    public static bool IsWalkable(
        in NavGraphData graph,
        NativeArray<bool> walkability,
        int index)
    {
        return walkability[graph.NodeOffset + index];
    }

    [BurstCompile]
    public static float GetNodeCost(
        in NavGraphData graph,
        NativeArray<float> staticCosts,
        int index)
    {
        return staticCosts[graph.NodeOffset + index];
    }

    // --- MATEMÁTICA INTERNA AUXILIAR (QuadSphere) ---

    [BurstCompile]
    private static CubeCoordinate DirectionToCubeCoordinate(float3 dir, int resolution)
    {
        dir = math.normalize(dir);
        float3 absDir = math.abs(dir);

        CubeFace face = CubeFace.PositiveZ;
        if (absDir.x >= absDir.y && absDir.x >= absDir.z)
            face = dir.x >= 0 ? CubeFace.PositiveX : CubeFace.NegativeX;
        else if (absDir.y >= absDir.x && absDir.y >= absDir.z)
            face = dir.y >= 0 ? CubeFace.PositiveY : CubeFace.NegativeY;
        else
            face = dir.z >= 0 ? CubeFace.PositiveZ : CubeFace.NegativeZ;

        float u = 0f, v = 0f;
        switch (face)
        {
            case CubeFace.PositiveX: u = (-dir.z / absDir.x + 1f) * 0.5f; v = (dir.y / absDir.x + 1f) * 0.5f; break;
            case CubeFace.NegativeX: u = (dir.z / absDir.x + 1f) * 0.5f; v = (dir.y / absDir.x + 1f) * 0.5f; break;
            case CubeFace.PositiveY: u = (dir.x / absDir.y + 1f) * 0.5f; v = (-dir.z / absDir.y + 1f) * 0.5f; break;
            case CubeFace.NegativeY: u = (dir.x / absDir.y + 1f) * 0.5f; v = (dir.z / absDir.y + 1f) * 0.5f; break;
            case CubeFace.PositiveZ: u = (dir.x / absDir.z + 1f) * 0.5f; v = (dir.y / absDir.z + 1f) * 0.5f; break;
            case CubeFace.NegativeZ: u = (-dir.x / absDir.z + 1f) * 0.5f; v = (dir.y / absDir.z + 1f) * 0.5f; break;
        }

        int x = math.clamp((int)math.floor(u * resolution), 0, resolution - 1);
        int y = math.clamp((int)math.floor(v * resolution), 0, resolution - 1);
        return new CubeCoordinate(face, x, y);
    }

    [BurstCompile]
    private static float3 CubeCoordinateToDirection(CubeCoordinate coord, int resolution)
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

    // --- REGIONES ---

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

                    return regionZ * (graph.RegionsPerRow * graph.RegionsPerCol)
                         + regionY * graph.RegionsPerRow
                         + regionX;
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

                    return (int)face * regionsPerFace
                         + regionY * graph.RegionsPerRow
                         + regionX;
                }

            default:
                return -1;
        }
    }
}