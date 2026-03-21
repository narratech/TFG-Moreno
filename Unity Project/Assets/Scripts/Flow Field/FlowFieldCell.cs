using Unity.VisualScripting;
using UnityEngine;

public class FlowFieldCell
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
}