using UnityEditor;
using UnityEngine;

[Icon("Assets/Gizmos/Grid-Icon.png")]
public class GridProvider : NavGraphProvider
{
    [Header("Settings")]
    [SerializeField] private int _width = 50;
    [SerializeField] private int _height = 50;
    [SerializeField] private int _regionWidth = 5;
    [SerializeField] private int _regionHeight = 5;
    [SerializeField] private float _cellSize = 1.0f;

    public Grid2DNavGraph GridGraph => Graph as Grid2DNavGraph;

    protected override bool ValidateConfiguration()
    {
        return _width > 0 && _height > 0 && _regionWidth > 0 && _regionHeight > 0 && _cellSize > 0f;
    }

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

    protected override void OnValidate()
    {
        base.OnValidate();
        _width = Mathf.Max(1, _width);
        _height = Mathf.Max(1, _height);
        _regionWidth = Mathf.Max(1, _regionWidth);
        _regionHeight = Mathf.Max(1, _regionHeight);
        _cellSize = Mathf.Max(0.1f, _cellSize);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGridAndRegions();

        if (Application.isPlaying && Graph != null && _drawWalkability)
        {
            DrawRuntimeGrid();
        }

#if UNITY_EDITOR
        DrawDebugLabels();
#endif
    }

    private void DrawGridAndRegions()
    {
        Vector3 origin = transform.position;
        float totalWidth = _width * _cellSize;
        float totalHeight = _height * _cellSize;

        Color cellColor = new Color(1f, 1f, 1f, 0.15f);
        Color regionColor = Color.blue;

        // Líneas a lo largo del eje Z (columnas X)
        for (int x = 0; x <= _width; x++)
        {
            bool isRegionBorder = (x % _regionWidth == 0) || (x == _width);
            Gizmos.color = isRegionBorder ? regionColor : cellColor;

            Vector3 start = origin + new Vector3(x * _cellSize, 0, 0);
            Vector3 end = start + new Vector3(0, 0, totalHeight);
            Gizmos.DrawLine(start, end);
        }

        // Líneas a lo largo del eje X (filas Z)
        for (int y = 0; y <= _height; y++)
        {
            bool isRegionBorder = (y % _regionHeight == 0) || (y == _height);
            Gizmos.color = isRegionBorder ? regionColor : cellColor;

            Vector3 start = origin + new Vector3(0, 0, y * _cellSize);
            Vector3 end = start + new Vector3(totalWidth, 0, 0);
            Gizmos.DrawLine(start, end);
        }
    }

    private void DrawRuntimeGrid()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Gizmos.color = Graph.IsWalkable(i) ? Color.cyan * 0.3f : Color.red * 0.5f;
            Gizmos.DrawCube(Graph.GetNodePosition(i), new Vector3(_cellSize * 0.9f, 0.05f, _cellSize * 0.9f));
        }
    }

#if UNITY_EDITOR
    private void DrawDebugLabels()
    {
        if (!_drawNodeIndices && !_drawCoordinates) return;

        Camera cam = SceneView.lastActiveSceneView?.camera;
        if (cam == null) return;

        Handles.color = Color.white;
        int totalNodes = Graph != null ? Graph.NodeCount : _width * _height;

        for (int i = 0; i < totalNodes; i++)
        {
            int x = i % _width;
            int y = i / _width;

            Vector3 pos = Graph != null
                ? Graph.GetNodePosition(i)
                : transform.position + new Vector3(x * _cellSize, 0, y * _cellSize);

            if (Vector3.Distance(cam.transform.position, pos) > 25f) continue;
            if (Vector3.Dot(cam.transform.forward, pos - cam.transform.position) <= 0f) continue;

            string text = "";
            if (_drawNodeIndices) text += $"[{i}]";
            if (_drawCoordinates)
            {
                if (!string.IsNullOrEmpty(text)) text += "\n";
                text += $"({x},{y})";
            }

            if (!string.IsNullOrEmpty(text))
            {
                Handles.Label(pos + Vector3.up * 0.05f, text);
            }
        }
    }
#endif
}