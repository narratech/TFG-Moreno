using UnityEngine;

public class GridProvider : NavGraphProvider
{
    [Header("Configuración del Grid 2D")]
    [SerializeField] private int _width = 50;
    [SerializeField] private int _height = 50;
    [SerializeField] private int _regionWidth = 5;
    [SerializeField] private int _regionHeight = 5;
    [SerializeField] private float _cellSize = 1.0f;

    [Header("Debug Settings")]
    [SerializeField] private bool _drawWalkability = true;
    [SerializeField] private bool _drawNodeIndices = false;
    [SerializeField] private bool _drawCoordinates = false;

    [Header("Scan Options")]
    [Range(0, 1)]
    [SerializeField] private float _scanFactor = 1.0f;

    public Grid2DNavGraph GridGraph => Graph as Grid2DNavGraph;

    protected override INavGraph CreateGraph()
    {
        return new Grid2DNavGraph(_width, _height, _cellSize, _regionWidth, _regionHeight, transform.position);
    }

    protected override void ScanObstacles()
    {
        float scanRadius = (_cellSize * 0.45f) * _scanFactor;

        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 worldPos = Graph.GetNodePosition(i);
            bool isObstacle = Physics.CheckSphere(worldPos, scanRadius, _obstacleMask);

            if (isObstacle)
            {
                Graph.SetWalkable(i, false);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.white;
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    Vector3 pos = transform.position + new Vector3(x * _cellSize, 0, y * _cellSize);
                    Gizmos.DrawWireCube(pos, new Vector3(_cellSize, 0.1f, _cellSize));
                }
            }

            Gizmos.color = Color.blue;
            for (int x = 0; x < _width; x += _regionWidth)
            {
                for (int y = 0; y < _height; y += _regionHeight)
                {
                    Vector3 pos = transform.position + new Vector3(x * _cellSize, 0, y * _cellSize);
                    Vector3 size = new Vector3(_regionWidth * _cellSize, 0.1f, _regionHeight * _cellSize);
                    Gizmos.DrawWireCube(pos + size / 2 - new Vector3(_cellSize / 2, 0, _cellSize / 2), size);
                }
            }
            return;
        }

        if (Graph == null) return;

        for (int i = 0; i < Graph.NodeCount; i++)
        {
            if (_drawWalkability)
            {
                Gizmos.color = Graph.IsWalkable(i) ? Color.cyan * 0.3f : Color.red * 0.5f;
                Gizmos.DrawCube(Graph.GetNodePosition(i), new Vector3(_cellSize * 0.9f, 0.1f, _cellSize * 0.9f));
            }
        }
    }
}