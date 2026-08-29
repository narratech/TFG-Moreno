using Unity.Collections;
using Unity.Mathematics;

public static class NavGraphFactory
{
    // <summary>
    // Creates a NavGraphData instance based on the provided INavGraph implementation.
    // </summary>
    public static NavGraphData CreateNavGraphData(INavGraph nav)
    {
        //Este switch case permite crear diferentes tipos de NavGraphData según el tipo de INavGraph proporcionado.
        // Si se crea un nuevo tipo de NavGraph, se debe agregar un nuevo case en este switch para manejarlo por datos.
        switch (nav)
        {
            case Grid2DNavGraph grid2D:
                return NavGraphFactory.CreateGrid2D(
                    grid2D.Width,
                    grid2D.Height,
                    grid2D.CellSize,
                    grid2D.RegionWidth,
                    grid2D.RegionHeight,
                    grid2D.Origin,
                    Allocator.Persistent);

            case Grid3DNavGraph grid3D:
                return NavGraphFactory.CreateGrid3D(
                    grid3D.Width,
                    grid3D.Height,
                    grid3D.Depth,
                    grid3D.CellSize,
                    grid3D.RegionWidth,
                    grid3D.RegionHeight,
                    grid3D.RegionDepth,
                    grid3D.Origin,
                    Allocator.Persistent);

            case QuadSphereNavGraph quadSphere:
                return NavGraphFactory.CreateQuadSphere(
                    quadSphere.Center,
                    quadSphere.Radius,
                    quadSphere.Rotation,
                    quadSphere.Resolution,
                    quadSphere.RegionsPerAxis,
                    Allocator.Persistent);

            default:
                throw new System.NotSupportedException(
                    $"Tipo de NavGraph no soportado: {nav.GetType().Name}");
        }
    }
    public static NavGraphData CreateGrid2D(
        int width, int height, float cellSize,
        int regW, int regH, float3 origin, Allocator allocator)
    {
        int count = width * height;
        var data = new NavGraphData
        {
            Type = NavGraphType.Grid2D,
            Width = width,
            Height = height,
            Depth = 1,
            CellSize = cellSize,
            Origin = origin,
            RegionWidth = regW,
            RegionHeight = regH,
            RegionsPerRow = (int)math.ceil((float)width / regW),
            RegionsPerCol = (int)math.ceil((float)height / regH)
        };
        return data;
    }

    public static NavGraphData CreateGrid3D(
        int width, int height, int depth, float cellSize,
        int regW, int regH, int regD, float3 origin, Allocator allocator)
    {
        int count = width * height * depth;
        var data = new NavGraphData
        {
            Type = NavGraphType.Grid3D,
            Width = width,
            Height = height,
            Depth = depth,
            CellSize = cellSize,
            Origin = origin,
            RegionWidth = regW,
            RegionHeight = regH,
            RegionDepth = regD,
            RegionsPerRow = (int)math.ceil((float)width / regW),
            RegionsPerCol = (int)math.ceil((float)height / regH),
            RegionsPerDepth = (int)math.ceil((float)depth / regD)
        };
        return data;
    }

    public static NavGraphData CreateQuadSphere(
        float3 center, float radius, quaternion rotation,
        int resolution, int regionsPerAxis, Allocator allocator)
    {
        int count = (resolution * resolution) * 6;
        var data = new NavGraphData
        {
            Type = NavGraphType.QuadSphere,
            Width = resolution,
            Height = resolution,
            Radius = radius,
            Origin = center,
            Rotation = rotation,
            RegionWidth = resolution / regionsPerAxis,
            RegionsPerRow = regionsPerAxis
        };
        return data;
    }
}