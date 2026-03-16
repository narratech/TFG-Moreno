using UnityEngine;

public class FlowFieldVisualizer : MonoBehaviour
{
    public FlowFieldGrid grid;
    public Color arrowColor = Color.darkGreen;

    public void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<FlowFieldGrid>();
        }
    }

    void OnDrawGizmos()
    {
        if (grid == null) return;

        Gizmos.color = arrowColor;
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                if (grid.Cells == null) continue;
                FlowFieldCell cell = grid.Cells[x, y];
                if (cell.direction != Vector2Int.zero)
                {
                    Vector2 dir = cell.direction.normalized;
                    // Draw arrow from cell center in the direction
                    float aux = grid.cellSize / 2f;
                    Vector3 start = cell.worldPos + new Vector3(aux,0.5f,aux);
                    Vector3 end = start + new Vector3(dir.x, 0, dir.y) * 3;
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawSphere(start, 0.4f);
                    Gizmos.DrawSphere(end, 0.2f);
                }
            }
        }

        Vector2 destination = grid.destination;
        FlowFieldCell destCell = grid.Cells[(int)destination.x, (int)destination.y];
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(destCell.worldPos + new Vector3(grid.cellSize / 2f, 0.5f, grid.cellSize / 2f), 2.0f);

    }
}
