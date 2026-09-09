using UnityEditor;
using UnityEngine;

[Icon("Assets/Gizmos/Voxel-Icon.png")]
public class VoxelProvider : NavGraphProvider
{
    [Header("Settings")]
    [SerializeField] private int _width = 30;
    [SerializeField] private int _height = 10;
    [SerializeField] private int _depth = 30;

    [SerializeField] private int _regionWidth = 5;
    [SerializeField] private int _regionHeight = 5;
    [SerializeField] private int _regionDepth = 5;

    [SerializeField] private float _cellSize = 1.0f;

    public Grid3DNavGraph VoxelGraph => Graph as Grid3DNavGraph;

    protected override bool ValidateConfiguration()
    {
        return _width > 0 && _height > 0 && _depth > 0 &&
               _regionWidth > 0 && _regionHeight > 0 && _regionDepth > 0 && _cellSize > 0f;
    }

    protected override INavGraph CreateGraph()
    {
        return new Grid3DNavGraph(
            _width, _height, _depth, _cellSize,
            _regionWidth, _regionHeight, _regionDepth,
            transform.position
        );
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

    protected override void OnValidate()
    {
        base.OnValidate();
        _width = Mathf.Max(1, _width);
        _height = Mathf.Max(1, _height);
        _depth = Mathf.Max(1, _depth);
        _regionWidth = Mathf.Max(1, _regionWidth);
        _regionHeight = Mathf.Max(1, _regionHeight);
        _regionDepth = Mathf.Max(1, _regionDepth);
        _cellSize = Mathf.Max(0.1f, _cellSize);
    }

    private void OnDrawGizmosSelected()
    {
        if (Graph == null)
        {
            DrawStaticGrid();
            DrawRegions();
        }
        else
        {
            if (_drawWalkability) DrawRuntimeGrid();
            DrawRegions();
        }

#if UNITY_EDITOR
        DrawDebugLabels();
#endif
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
                    Vector3 pos = transform.position + new Vector3(x * _cellSize, y * _cellSize, z * _cellSize);
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

            Gizmos.DrawCube(Graph.GetNodePosition(i), Vector3.one * (_cellSize * 0.9f));
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
                    Vector3 pos = transform.position + new Vector3(x * _cellSize, y * _cellSize, z * _cellSize);
                    Vector3 size = new Vector3(_regionWidth * _cellSize, _regionHeight * _cellSize, _regionDepth * _cellSize);

                    Gizmos.DrawWireCube(pos + size / 2f - Vector3.one * (_cellSize / 2f), size);
                }
            }
        }
    }

#if UNITY_EDITOR
    private void DrawDebugLabels()
    {
        if (!_drawNodeIndices && !_drawCoordinates) return;

        Camera cam = SceneView.lastActiveSceneView?.camera;
        if (cam == null) return;

        Handles.color = Color.white;
        int totalNodes = Graph != null ? Graph.NodeCount : _width * _height * _depth;
        int areaXY = _width * _height;

        for (int i = 0; i < totalNodes; i++)
        {
            int z = i / areaXY;
            int remainder = i % areaXY;
            int y = remainder / _width;
            int x = remainder % _width;

            Vector3 pos = Graph != null
                ? Graph.GetNodePosition(i)
                : transform.position + new Vector3(x * _cellSize, y * _cellSize, z * _cellSize);

            // Optimización de distancia y visibilidad
            if (Vector3.Distance(cam.transform.position, pos) > 20f) continue;
            if (Vector3.Dot(cam.transform.forward, pos - cam.transform.position) <= 0f) continue;

            string text = "";
            if (_drawNodeIndices) text += $"[{i}]";
            if (_drawCoordinates)
            {
                if (!string.IsNullOrEmpty(text)) text += "\n";
                text += $"({x},{y},{z})";
            }

            if (!string.IsNullOrEmpty(text))
            {
                Handles.Label(pos, text);
            }
        }
    }
#endif
}