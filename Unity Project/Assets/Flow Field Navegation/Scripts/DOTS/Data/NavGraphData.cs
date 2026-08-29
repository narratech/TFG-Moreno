using System;
using Unity.Collections;
using Unity.Mathematics;

public enum NavGraphType : byte
{
    Grid2D,
    Grid3D,
    QuadSphere
}

public struct NavGraphData
{
    public NavGraphType Type;
    public int GraphId;

    // --- Geometría y Dimensiones Generales ---
    public int Width;
    public int Height;
    public int Depth;
    public float CellSize;
    public float3 Origin;

    // --- Específico de QuadSphere ---
    public float Radius;
    public quaternion Rotation;

    // --- Datos de Clustering / Regiones ---
    public int RegionWidth;
    public int RegionHeight;
    public int RegionDepth;
    public int RegionsPerRow;
    public int RegionsPerCol;
    public int RegionsPerDepth;

    // --- Memoria Nativa (Zero GC) ---
    public int NodeOffset;

    public readonly int NodeCount => Type switch
    {
        NavGraphType.Grid2D => Width * Height,
        NavGraphType.Grid3D => Width * Height * Depth,
        NavGraphType.QuadSphere => (Width * Width) * 6, // Width = Resolution
        _ => 0
    };

    public readonly int RegionCount => Type switch
    {
        NavGraphType.Grid2D => RegionsPerRow * RegionsPerCol,
        NavGraphType.Grid3D => RegionsPerRow * RegionsPerCol * RegionsPerDepth,
        NavGraphType.QuadSphere => (RegionsPerRow * RegionsPerRow) * 6,
        _ => 0
    };
}