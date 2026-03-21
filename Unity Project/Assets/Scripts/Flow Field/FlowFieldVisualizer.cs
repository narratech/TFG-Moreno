using UnityEngine;

public class FlowFieldVisualizer : MonoBehaviour
{
    public FlowFieldGrid grid;
    public Mesh arrowMesh;
    public Material arrowMaterial;
    public float arrowsScale = 0.5f;
    public bool showGizmos = true;

    public void Awake()
    {
        if (grid == null)
        {
            grid = GetComponent<FlowFieldGrid>();
        }
    }

    void Update()
    {
        if (grid == null || grid.Cells == null || !showGizmos) return;

        Vector3 scale = Vector3.one * grid.cellSize * arrowsScale;

        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                FlowFieldCell cell = grid.Cells[x, y];
                if (cell.direction == Vector2Int.zero) continue;

                Vector2 dir = cell.direction.normalized;
                Vector3 dir3D = new Vector3(dir.x, 0, dir.y);

                Vector3 pos = cell.worldPos + new Vector3(0, 0.5f, 0);

                Quaternion rot = Quaternion.LookRotation(dir3D);

                Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, scale);

                Graphics.DrawMesh(arrowMesh, matrix, arrowMaterial, 0);
            }
        }
    }
}
