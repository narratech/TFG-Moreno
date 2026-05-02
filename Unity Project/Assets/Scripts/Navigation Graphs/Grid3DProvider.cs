using UnityEngine;

public class Grid3DProvider : MonoBehaviour
{
    [Header("Configuración del Grid 3D")]
    [SerializeField] private int _width = 30;
    [SerializeField] private int _height = 10;
    [SerializeField] private int _depth = 30;

    [SerializeField] private int _regionWidth = 5;
    [SerializeField] private int _regionHeight = 5;
    [SerializeField] private int _regionDepth = 5;

    [SerializeField] private float _cellSize = 1.0f;

    [SerializeField] private LayerMask _obstacleMask;

    public Grid3DNavGraph Graph { get; private set; }

    private void Awake()
    {
        InitializeGraph();
    }

    public void InitializeGraph()
    {
        if (Graph != null) return;

        Graph = new Grid3DNavGraph(
            _width,
            _height,
            _depth,
            _cellSize,
            _regionWidth,
            _regionHeight,
            _regionDepth,
            transform.position
        );

        ScanObstacles();

        FlowFieldManager.Instance.RegisterContext(Graph);
    }
    private void ScanObstacles()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 worldPos = Graph.GetNodePosition(i);

            bool isObstacle = Physics.CheckSphere(
                worldPos,
                _cellSize * 0.45f,
                _obstacleMask
            );

            if (isObstacle)
            {
                Graph.SetWalkable(i, false);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (Graph == null)
        {
            DrawStaticGrid();
            return;
        }

        DrawRuntimeGrid();
        DrawRegions();
    }

    private void DrawStaticGrid()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int z = 0; z < _depth; z++)
                {
                    Vector3 pos = transform.position + new Vector3(
                        x * _cellSize,
                        y * _cellSize,
                        z * _cellSize
                    );

                    Gizmos.DrawWireCube(pos, Vector3.one * _cellSize);
                }
            }
        }
    }

    private void DrawRuntimeGrid()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Gizmos.color = Graph.IsWalkable(i)
                ? new Color(0, 1, 1, 0.2f)
                : new Color(1, 0, 0, 0.5f);

            Gizmos.DrawCube(
                Graph.GetNodePosition(i),
                Vector3.one * (_cellSize * 0.9f)
            );
        }
    }

    private void DrawRegions()
    {
        Gizmos.color = Color.blue;

        for (int x = 0; x < _width; x += _regionWidth)
        {
            for (int y = 0; y < _height; y += _regionHeight)
            {
                for (int z = 0; z < _depth; z += _regionDepth)
                {
                    Vector3 pos = transform.position + new Vector3(
                        x * _cellSize,
                        y * _cellSize,
                        z * _cellSize
                    );

                    Vector3 size = new Vector3(
                        _regionWidth * _cellSize,
                        _regionHeight * _cellSize,
                        _regionDepth * _cellSize
                    );

                    Gizmos.DrawWireCube(
                        pos + size / 2 - Vector3.one * (_cellSize / 2),
                        size
                    );
                }
            }
        }
    }
}