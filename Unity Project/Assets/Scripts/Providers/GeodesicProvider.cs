using System;
using UnityEditor;
using UnityEngine;

public class GeodesicProvider : NavGraphProvider
{
    [Header("Configuración de Geodésica (QuadSphere)")]
    [SerializeField] private float _radius = 10f;
    [SerializeField] private int _resolution = 32;
    [SerializeField] private int _regionsPerAxis = 4;

    [Header("Debug Settings")]
    [SerializeField] private bool _drawWalkability = true;
    [SerializeField] private bool _drawNodeIndices = true;
    [SerializeField] private bool _drawCoordinates = true;
    [SerializeField] private bool _drawFace = true;

    [Header("Scan Options")]
    [Range(0, 1)]
    [SerializeField] private float _scanFactor = 1.0f;

    private int _regionSize => _resolution / _regionsPerAxis;

    // Propiedad tipada opcional para el grafo geodésico
    public QuadSphereNavGraph GeodesicGraph => Graph as QuadSphereNavGraph;

    protected override INavGraph CreateGraph()
    {
        return new QuadSphereNavGraph(
            transform.position,
            _radius,
            transform.rotation,
            _resolution,
            _regionsPerAxis
        );
    }

    protected override void ScanObstacles()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 position = Graph.GetNodePosition(i);
            Vector3 normal = position - transform.position;
            Vector3 pos = position + normal.normalized * 100 * _scanFactor;

            if (Physics.Raycast(pos, -normal, 100 * _scanFactor, _obstacleMask))
            {
                Graph.SetWalkable(i, false);
            }
        }
    }

    private void OnValidate()
    {
        _regionsPerAxis = GetNearestValidRegionsPerAxis(_resolution, _regionsPerAxis);
    }

    private static int GetNearestValidRegionsPerAxis(int resolution, int requested)
    {
        requested = Mathf.Clamp(requested, 1, resolution);

        int best = 1;
        int bestDistance = int.MaxValue;

        for (int i = 1; i <= resolution; i++)
        {
            if (resolution % i != 0) continue;

            int distance = Mathf.Abs(i - requested);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
        {
            for (int y = 0; y < _resolution; y++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    GetCellCorners(face, x, y, out Vector3 p00, out Vector3 p10, out Vector3 p11, out Vector3 p01);

                    if (y == 0)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawLine(p00, p10);
                    }

                    if (x == 0)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawLine(p00, p01);
                    }

                    bool rightRegionBorder = (x + 1) % _regionSize == 0 || x == _resolution - 1;
                    Gizmos.color = rightRegionBorder ? Color.blue : new Color(1f, 1f, 1f, 0.25f);
                    Gizmos.DrawLine(p10, p11);

                    bool topRegionBorder = (y + 1) % _regionSize == 0 || y == _resolution - 1;
                    Gizmos.color = topRegionBorder ? Color.blue : new Color(1f, 1f, 1f, 0.25f);
                    Gizmos.DrawLine(p01, p11);

                    if (Graph != null && _drawWalkability)
                    {
                        int index = GeodesicGraph.CoordinateToIndex(new CubeCoordinate(face, x, y));
                        Color color = Graph.IsWalkable(index)
                            ? new Color(0f, 1f, 0f, 0.15f)
                            : new Color(1f, 0f, 0f, 0.35f);

#if UNITY_EDITOR
                        DrawQuad(p00, p10, p11, p01, color);
#endif
                    }
                }
            }
        }

#if UNITY_EDITOR
        DrawDebugLabels();
#endif
    }

#if UNITY_EDITOR
    private void DrawQuad(Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01, Color color)
    {
        Handles.color = color;
        Handles.DrawAAConvexPolygon(p00, p10, p11, p01);
    }

    private void DrawDebugLabels()
    {
        if (GeodesicGraph == null) return;

        Camera cam = SceneView.lastActiveSceneView?.camera;
        if (cam == null) return;

        Handles.color = Color.white;

        for (int i = 0; i < Graph.NodeCount; i++)
        {
            CubeCoordinate coord = GeodesicGraph.IndexToCoordinate(i);
            Vector3 pos = Graph.GetNodePosition(i);
            Vector3 normal = (pos - transform.position).normalized;

            Vector3 toCamera = (cam.transform.position - pos).normalized;
            if (Vector3.Dot(normal, toCamera) <= 0f) continue;

            pos += normal * 0.05f;
            string text = "";

            if (_drawNodeIndices) text += $"[{i}]";
            if (_drawFace) text += $"\n{coord.Face}";
            if (_drawCoordinates) text += $"\n({coord.X},{coord.Y})";

            Handles.Label(pos, text);
        }
    }
#endif

    private void GetCellCorners(CubeFace face, int x, int y, out Vector3 p00, out Vector3 p10, out Vector3 p11, out Vector3 p01)
    {
        float inv = 1f / _resolution;
        p00 = GetCorner(face, x * inv, y * inv);
        p10 = GetCorner(face, (x + 1) * inv, y * inv);
        p11 = GetCorner(face, (x + 1) * inv, (y + 1) * inv);
        p01 = GetCorner(face, x * inv, (y + 1) * inv);
    }

    private Vector3 GetCorner(CubeFace face, float u, float v)
    {
        Vector3 direction = CubeProjection.UVToDirection(face, u, v);
        return CubeProjection.DirectionToWorld(transform.position, _radius, transform.rotation, direction);
    }
}