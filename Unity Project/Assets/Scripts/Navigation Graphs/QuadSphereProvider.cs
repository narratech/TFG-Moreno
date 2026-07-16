using System;
using UnityEditor;
using UnityEngine;

public class QuadSphereProvider : MonoBehaviour
{
    [Header("Configuracion de QuadSphere")]
    [SerializeField] private float _radius = 10f;
    [SerializeField] private int _resolution = 32;
    [SerializeField] private int _regionsPerAxis = 4;

    [SerializeField] private bool _drawNodeIndices = true;
    [SerializeField] private bool _drawCoordinates = true;
    [SerializeField] private bool _drawFace = true;

    private int _regionSize => _resolution / _regionsPerAxis;

    [Header("Obstacles")]
    [SerializeField] private LayerMask _obstacleMask;

    public QuadSphereNavGraph Graph { get; private set; }

    private void Awake()
    {
        InitializeGraph();
    }

    public void InitializeGraph()
    {
        if (Graph != null)
            return;

        Graph = new QuadSphereNavGraph(
            transform.position,
            _radius,
            transform.rotation,
            _resolution,
            _regionsPerAxis);

        ScanObstacles();

        FlowFieldManager.Instance.RegisterContext(Graph);
    }

    private void ScanObstacles()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 position = Graph.GetNodePosition(i);

            float nodeRadius = Graph.GetNodeSize(i).x * 0.45f;

            bool blocked = Physics.CheckSphere(
                position,
                nodeRadius,
                _obstacleMask);

            Graph.SetWalkable(i, !blocked);
        }
    }

    private void OnDrawGizmosSelected()
    {
        foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
        {
            for (int y = 0; y < _resolution; y++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    GetCellCorners(
                        face,
                        x,
                        y,
                        out Vector3 p00,
                        out Vector3 p10,
                        out Vector3 p11,
                        out Vector3 p01);

                    // Borde inferior (solo primera fila)
                    if (y == 0)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawLine(p00, p10);
                    }

                    // Borde izquierdo (solo primera columna)
                    if (x == 0)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawLine(p00, p01);
                    }

                    // Borde derecho
                    bool rightRegionBorder =
                        (x + 1) % _regionSize == 0 ||
                        x == _resolution - 1;

                    Gizmos.color = rightRegionBorder
                        ? Color.blue
                        : new Color(1f, 1f, 1f, 0.25f);

                    Gizmos.DrawLine(p10, p11);

                    // Borde superior
                    bool topRegionBorder =
                        (y + 1) % _regionSize == 0 ||
                        y == _resolution - 1;

                    Gizmos.color = topRegionBorder
                        ? Color.blue
                        : new Color(1f, 1f, 1f, 0.25f);

                    Gizmos.DrawLine(p01, p11);
                }
            }
        }

        DrawDebugLabels();
    }

    private void DrawQuadSphereGrid(int resolution, Color color)
    {
        Gizmos.color = color;

        foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    DrawCell(face, x, y);
                }
            }
        }
    }

    private void DrawCell(CubeFace face, int x, int y)
    {
        GetCellCorners(face, x, y,
            out Vector3 p00,
            out Vector3 p10,
            out Vector3 p11,
            out Vector3 p01);

        Gizmos.DrawLine(p00, p10);
        Gizmos.DrawLine(p10, p11);
        Gizmos.DrawLine(p11, p01);
        Gizmos.DrawLine(p01, p00);
    }

    private void GetCellCorners(
    CubeFace face,
    int x,
    int y,
    out Vector3 p00,
    out Vector3 p10,
    out Vector3 p11,
    out Vector3 p01)
    {
        float inv = 1f / _resolution;

        p00 = GetCorner(face, x * inv, y * inv);
        p10 = GetCorner(face, (x + 1) * inv, y * inv);
        p11 = GetCorner(face, (x + 1) * inv, (y + 1) * inv);
        p01 = GetCorner(face, x * inv, (y + 1) * inv);
    }

    private Vector3 GetCorner(
    CubeFace face,
    float u,
    float v)
    {
        Vector3 direction =
            CubeProjection.UVToDirection(
                face, u, v);

        return CubeProjection.DirectionToWorld(
            transform.position,
            _radius,
            transform.rotation,
            direction);
    }

    private void OnValidate()
    {
        _regionsPerAxis = GetNearestValidRegionsPerAxis(
            _resolution,
            _regionsPerAxis);
    }

    private static int GetNearestValidRegionsPerAxis(
    int resolution,
    int requested)
    {
        requested = Mathf.Clamp(requested, 1, resolution);

        int best = 1;
        int bestDistance = int.MaxValue;

        for (int i = 1; i <= resolution; i++)
        {
            if (resolution % i != 0)
                continue;

            int distance = Mathf.Abs(i - requested);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    private void DrawDebugLabels()
    {
        if (Graph == null)
            return;

        Camera cam = SceneView.lastActiveSceneView?.camera;

        if (cam == null)
            return;

        Handles.color = Color.white;

        for (int i = 0; i < Graph.NodeCount; i++)
        {
            CubeCoordinate coord = Graph.IndexToCoordinate(i);

            Vector3 pos = Graph.GetNodePosition(i);
            Vector3 normal = (pos - transform.position).normalized;

            // Si la cara mira en sentido contrario a la cámara, no la dibujamos.
            Vector3 toCamera = (cam.transform.position - pos).normalized;

            if (Vector3.Dot(normal, toCamera) <= 0f)
                continue;

            pos += normal * 0.05f;

            string text = "";

            if (_drawNodeIndices)
                text += $"[{i}]";

            if (_drawFace)
                text += $"\n{coord.Face}";

            if (_drawCoordinates)
                text += $"\n({coord.X},{coord.Y})";

            Handles.Label(pos, text);
        }
    }
#endif
}