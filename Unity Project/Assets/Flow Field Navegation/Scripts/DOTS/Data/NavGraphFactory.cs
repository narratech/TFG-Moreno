using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;

public static class NavGraphFactory
{
    /// <summary>
    /// Crea una instancia de NavGraphData basada en la implementación de INavGraph proporcionada.
    /// </summary>
    public static NavGraphData CreateNavGraphData(INavGraph nav)
    {
        NavGraphData graphData;
        switch (nav)
        {
            case Grid2DNavGraph grid2D:
                graphData = CreateGrid2D(
                    grid2D.Width,
                    grid2D.Height,
                    grid2D.CellSize,
                    grid2D.RegionWidth,
                    grid2D.RegionHeight,
                    grid2D.Origin,
                    Allocator.Persistent);
                break;

            case Grid3DNavGraph grid3D:
                graphData = CreateGrid3D(
                    grid3D.Width,
                    grid3D.Height,
                    grid3D.Depth,
                    grid3D.CellSize,
                    grid3D.RegionWidth,
                    grid3D.RegionHeight,
                    grid3D.RegionDepth,
                    grid3D.Origin,
                    Allocator.Persistent);
                break;

            case QuadSphereNavGraph quadSphere:
                graphData = CreateQuadSphere(
                    quadSphere.Center,
                    quadSphere.Radius,
                    quadSphere.Rotation,
                    quadSphere.Resolution,
                    quadSphere.RegionsPerAxis,
                    Allocator.Persistent);
                break;
            default:
                throw new NotSupportedException($"Tipo de NavGraph no soportado: {nav.GetType().Name}");
        }

        NativeArray<float> staticCosts = new NativeArray<float>(nav.NodeCount, Allocator.Persistent);
        for (int i = 0; i < nav.NodeCount; i++)
        {
            staticCosts[i] = nav.GetNodeCost(i);
        }

        NativeArray<bool> walkability = new NativeArray<bool>(nav.NodeCount, Allocator.Persistent);
        for (int i = 0; i < nav.NodeCount; i++)
        {
            walkability[i] = nav.IsWalkable(i);
        }

        graphData.GraphId = nav.GraphId;

        FlowFieldStorage.Instance.RegisterNavGraphData(graphData, staticCosts, walkability);

        staticCosts.Dispose();
        walkability.Dispose();

        return graphData;
    }

    public static NavGraphData CreateGrid2D(
        int width, int height, float cellSize,
        int regW, int regH, float3 origin, Allocator allocator)
    {
        int totalNodes = width * height;
        int regRow = (int)math.ceil((float)width / regW);
        int regCol = (int)math.ceil((float)height / regH);
        int totalRegions = regRow * regCol;

        return new NavGraphData
        {
            Type = NavGraphType.Grid2D,
            Width = width,
            Height = height,
            Depth = 1,
            CellSize = cellSize,
            Radius = 0f,
            Origin = origin,
            Rotation = quaternion.identity,
            RegionWidth = regW,
            RegionHeight = regH,
            RegionDepth = 1,
            RegionsPerRow = regRow,
            RegionsPerCol = regCol,
            RegionsPerDepth = 1,
            NodeOffset = 0
        };
    }

    public static NavGraphData CreateGrid3D(
        int width, int height, int depth, float cellSize,
        int regW, int regH, int regD, float3 origin, Allocator allocator)
    {
        int totalNodes = width * height * depth;
        int regRow = (int)math.ceil((float)width / regW);
        int regCol = (int)math.ceil((float)height / regH);
        int regDepth = (int)math.ceil((float)depth / regD);
        int totalRegions = regRow * regCol * regDepth;

        return new NavGraphData
        {
            Type = NavGraphType.Grid3D,
            Width = width,
            Height = height,
            Depth = depth,
            CellSize = cellSize,
            Radius = 0f,
            Origin = origin,
            Rotation = quaternion.identity,
            RegionWidth = regW,
            RegionHeight = regH,
            RegionDepth = regD,
            RegionsPerRow = regRow,
            RegionsPerCol = regCol,
            RegionsPerDepth = regDepth,
            NodeOffset = 0
        };
    }

    public static NavGraphData CreateQuadSphere(
        float3 center, float radius, quaternion rotation,
        int resolution, int regionsPerAxis, Allocator allocator)
    {
        int regW = resolution / regionsPerAxis;
        int regH = resolution / regionsPerAxis;
        int totalNodes = (resolution * resolution) * 6;
        int totalRegions = (regionsPerAxis * regionsPerAxis) * 6;

        return new NavGraphData
        {
            Type = NavGraphType.QuadSphere,
            Width = resolution,
            Height = resolution,
            Depth = 1,
            CellSize = (radius * math.PI * 2f) / (resolution * 4f),
            Radius = radius,
            Origin = center,
            Rotation = rotation,
            RegionWidth = regW,
            RegionHeight = regH,
            RegionDepth = 1,
            RegionsPerRow = regionsPerAxis,
            RegionsPerCol = regionsPerAxis,
            RegionsPerDepth = 1,
            NodeOffset = 0
        };
    }
}