using System.Collections.Generic;
using UnityEngine;

public class FlowFieldGrid : MonoBehaviour
{
    public int width = 50;
    public int height = 50;
    public float cellSize = 2f;
    public Terrain terrain;
    public Vector2Int destination;

    private FlowFieldCell[,] cells;

    void Start()
    {
        GenerateGrid();
        GenerateFlowfield(destination);
    }

    // 1. Generar grid sobre el terrain
    void GenerateGrid()
    {
        cells = new FlowFieldCell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 worldPos = new Vector3(x * cellSize, 0, y * cellSize);
                bool isObstacle = false;
                if (terrain != null)
                {
                    float heightSample = terrain.SampleHeight(worldPos);
                    worldPos.y = heightSample;

                    RaycastHit hit;
                    if (Physics.Raycast(worldPos + Vector3.up * 10, Vector3.down, out hit, 20f))
                    {
                        float angle = Vector3.Angle(hit.normal, Vector3.up);
                        if (angle > 30f) // umbral de inclinación
                        {
                            isObstacle = true;
                        }
                    }
                }
                cells[x, y] = new FlowFieldCell(worldPos);
                cells[x, y].isObstacle = isObstacle;
            }
        }
    }

    // 2. BFS para generar campo de costos y direcciones
    public void GenerateFlowfield(Vector2Int target)
    {
        foreach (var cell in cells)
        {
            cell.cost = int.MaxValue;
        }
         
        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        cells[target.x, target.y].cost = 0;
        frontier.Enqueue(target);

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int currentCost = cells[current.x, current.y].cost;
            Vector2 currDir = cells[current.x, current.y].direction;

            foreach (Vector2Int dir in Neighbors())
            {
                Vector2Int neighbor = current + dir;
                if (IsInside(neighbor))
                {
                    int newCost = currentCost + 1;
                    Vector2 addDir = currDir - dir;
                    if (dir.x == 0)
                    {
                        addDir.x = 0;
                    }
                    else {
                        addDir.y = 0;
                    }

                    if (newCost < cells[neighbor.x, neighbor.y].cost)
                    {
                        cells[neighbor.x, neighbor.y].cost = newCost;
                        frontier.Enqueue(neighbor);
                    }
                    if (currentCost < cells[neighbor.x, neighbor.y].cost)
                    {
                        cells[neighbor.x, neighbor.y].direction += addDir;
                    }
                }
            }
        }
    }

    // Helpers
    bool IsInside(Vector2Int pos)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < width && pos.y < height && !cells[pos.x, pos.y].isObstacle;
    }

    List<Vector2Int> Neighbors()
    {
        return new List<Vector2Int> {
            new Vector2Int(1,0), new Vector2Int(-1,0),
            new Vector2Int(0,1), new Vector2Int(0,-1)
        };
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.x / cellSize), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(worldPos.z / cellSize), 0, height - 1);
        return new Vector2Int(x, y);
    }
    public FlowFieldCell GetCellAtWorldPos(Vector3 worldPos)
    {
        Vector2Int cellIndex = WorldToCell(worldPos);
        return cells[cellIndex.x, cellIndex.y];
    }

    public FlowFieldCell[,] Cells
    {
        get { return cells; }
    }
    public bool IsDestination(FlowFieldCell cell)
    {
        Vector2Int cellIndex = WorldToCell(cell.worldPos);
        return cellIndex == destination;
    }

    public Vector3 DestinationWorldCentre()
    {
        Vector3 centre = new Vector3(
            (destination.x + 0.5f) * cellSize,
            0,
            (destination.y + 0.5f) * cellSize
        );
        if (terrain != null)
        {
            centre.y = terrain.SampleHeight(centre);
        }
        return centre;
    }
}
