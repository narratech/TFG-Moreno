using UnityEditor;
using UnityEngine;

[Icon("Assets/Gizmos/Voxel-Icon.png")]
public class VoxelProvider : NavGraphProvider
{
    [Header("Configuración del Voxel 3D")]
    [SerializeField] private int _width = 30;
    [SerializeField] private int _height = 10;
    [SerializeField] private int _depth = 30;

    [SerializeField] private int _regionWidth = 5;
    [SerializeField] private int _regionHeight = 5;
    [SerializeField] private int _regionDepth = 5;

    [SerializeField] private float _cellSize = 1.0f;

    [Header("Voxel Visual Filter Settings")]

    [Tooltip("Permite inspeccionar solo una capa Y concreta en lugar de todo el volumen 3D.")]
    [SerializeField] private bool _enableSliceFilter = false;
    [SerializeField] private int _sliceY = 0;

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
        _sliceY = Mathf.Clamp(_sliceY, 0, _height - 1);
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
        float totalDepth = _depth * _cellSize;

        Color regionColor = Color.blue;
        Color cellColor = new Color(1f, 1f, 1f, 0.12f);

        int minY = _enableSliceFilter ? _sliceY : 0;
        int maxY = _enableSliceFilter ? _sliceY + 1 : _height;

        // 1. Líneas paralelas al eje X
        for (int y = minY; y <= maxY; y++)
        {
            bool isYRegion = (y % _regionHeight == 0) || (y == _height);

            for (int z = 0; z <= _depth; z++)
            {
                bool isZRegion = (z % _regionDepth == 0) || (z == _depth);

                bool shouldDraw = _enableSliceFilter || isYRegion || isZRegion;
                if (!shouldDraw) continue;

                // En modo Slice, cualquier borde en Z actúa como límite de región en esa capa
                bool isRegionFrame = (isYRegion && isZRegion) || (_enableSliceFilter && isZRegion);
                Gizmos.color = isRegionFrame ? regionColor : cellColor;

                Vector3 start = origin + new Vector3(0, y * _cellSize, z * _cellSize);
                Gizmos.DrawLine(start, start + new Vector3(totalWidth, 0, 0));
            }
        }

        // 2. Líneas paralelas al eje Y (Verticales)
        for (int x = 0; x <= _width; x++)
        {
            bool isXRegion = (x % _regionWidth == 0) || (x == _width);

            for (int z = 0; z <= _depth; z++)
            {
                bool isZRegion = (z % _regionDepth == 0) || (z == _depth);

                bool shouldDraw = _enableSliceFilter || isXRegion || isZRegion;
                if (!shouldDraw) continue;

                // En modo Slice, los postes verticales en paredes o esquinas de región se pintan en azul
                bool isRegionFrame = (isXRegion && isZRegion) || (_enableSliceFilter && (isXRegion || isZRegion));
                Gizmos.color = isRegionFrame ? regionColor : cellColor;

                Vector3 start = origin + new Vector3(x * _cellSize, minY * _cellSize, z * _cellSize);
                Vector3 end = origin + new Vector3(x * _cellSize, maxY * _cellSize, z * _cellSize);
                Gizmos.DrawLine(start, end);
            }
        }

        // 3. Líneas paralelas al eje Z
        for (int x = 0; x <= _width; x++)
        {
            bool isXRegion = (x % _regionWidth == 0) || (x == _width);

            for (int y = minY; y <= maxY; y++)
            {
                bool isYRegion = (y % _regionHeight == 0) || (y == _height);

                bool shouldDraw = _enableSliceFilter || isXRegion || isYRegion;
                if (!shouldDraw) continue;

                // En modo Slice, cualquier borde en X actúa como límite de región en esa capa
                bool isRegionFrame = (isXRegion && isYRegion) || (_enableSliceFilter && isXRegion);
                Gizmos.color = isRegionFrame ? regionColor : cellColor;

                Vector3 start = origin + new Vector3(x * _cellSize, y * _cellSize, 0);
                Gizmos.DrawLine(start, start + new Vector3(0, 0, totalDepth));
            }
        }
    }

    private void DrawRuntimeGrid()
    {
        int areaXY = _width * _height;

        for (int i = 0; i < Graph.NodeCount; i++)
        {
            int z = i / areaXY;
            int remainder = i % areaXY;
            int y = remainder / _width;

            // Si está activado el filtro por capa, omitir nodos de otros niveles Y
            if (_enableSliceFilter && y != _sliceY) continue;

            bool isWalkable = Graph.IsWalkable(i);

            Gizmos.color = isWalkable
                ? new Color(0, 1, 1, 0.2f)
                : new Color(1, 0, 0, 0.5f);

            Gizmos.DrawCube(Graph.GetNodePosition(i), Vector3.one * (_cellSize * 0.9f));
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

            if (_enableSliceFilter && y != _sliceY) continue;

            Vector3 pos = Graph != null
                ? Graph.GetNodePosition(i)
                : transform.position + new Vector3(x * _cellSize, y * _cellSize, z * _cellSize);

            if (Vector3.Distance(cam.transform.position, pos) > 15f) continue;
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