using Unity.Mathematics;
using UnityEngine;

public struct FlowFieldCell
{
    public Vector3 worldPos;
    public int cost;
    public Vector2 direction;
    public bool isObstacle;

    public FlowFieldCell(Vector3 pos)
    {
        worldPos = pos;
        cost = int.MaxValue; // infinito al inicio
        direction = Vector2.zero;
        isObstacle = false;
    }

    // operador de igualdad para comparar celdas
    public static bool operator ==(FlowFieldCell a, FlowFieldCell b)
    {
        return a.worldPos == b.worldPos;
    }
    public static bool operator !=(FlowFieldCell a, FlowFieldCell b)
    {
        return !(a == b);
    }
}

public struct FlowFieldCellData
{
    public float3 worldPos;       // posición en el mundo
    public int cost;              // coste de la celda
    public float2 direction;      // dirección del flow field
    public byte isObstacle;       // 0 = libre, 1 = obstáculo

    public FlowFieldCellData(float3 pos)
    {
        worldPos = pos;
        cost = int.MaxValue;
        direction = float2.zero;
        isObstacle = 0;
    }
}