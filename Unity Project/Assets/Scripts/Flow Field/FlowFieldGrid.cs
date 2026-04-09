using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

public class FlowFieldGrid : MonoBehaviour
{
    public int width = 50;
    public int height = 50;
    public float cellSize = 2f;
    public Vector2Int destination;

    public LayerMask terrainLayer; // para raycast al generar grid

    private FlowFieldCell[,] cells;

    // Patron singleton para acceso global
    public static FlowFieldGrid Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        GenerateGrid();
    }

    // 1. Generar grid sobre el terrain
    void GenerateGrid()
    {
        cells = new FlowFieldCell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 origin = new Vector3(x * cellSize + (cellSize/2), 100f, y * cellSize + (cellSize / 2)); // desde arriba
                RaycastHit hit;

                bool hitSomething = Physics.Raycast(origin, Vector3.down, out hit, 200f, terrainLayer);

                bool isOnNavMesh = false;
                Vector3 worldPos = origin;

                if (hitSomething)
                {
                    NavMeshHit navHit;

                    if (NavMesh.SamplePosition(hit.point, out navHit, cellSize * 0.5f, NavMesh.AllAreas))
                    {
                        isOnNavMesh = true;
                        worldPos = navHit.position;
                    }
                    else
                    {
                        worldPos = hit.point;
                    }
                }

                // Crear celda
                cells[x, y] = new FlowFieldCell(worldPos);
                cells[x, y].isObstacle = !isOnNavMesh;
            }
        }
    }

    // 2. BFS para generar campo de costos y direcciones
    public void GenerateFlowfield(Vector2Int target)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y].cost = int.MaxValue;
                cells[x, y].direction = Vector2.zero;
            }
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
        Vector3 centre = Cells[destination.x, destination.y].worldPos;
        return centre;
    }

    public void SetDestination(Vector3 worldPos)
    {
        Vector2Int newDest = WorldToCell(worldPos);
        if (IsInside(newDest))
        {
            destination = newDest;
            GenerateFlowfield(destination);
        }
    }
    public NativeArray<FlowFieldCellData> GetNativeCells(Allocator allocator)
    {
        NativeArray<FlowFieldCellData> nativeCells = new NativeArray<FlowFieldCellData>(width * height, allocator);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = x + y * width;
                FlowFieldCell cell = cells[x, y];
                nativeCells[index] = new FlowFieldCellData
                {
                    cost = cell.cost,
                    direction = cell.direction,
                    isObstacle = (byte)(cell.isObstacle ? 1 : 0),
                    worldPos = cell.worldPos
                };
            }
        }

        return nativeCells;
    }
}
