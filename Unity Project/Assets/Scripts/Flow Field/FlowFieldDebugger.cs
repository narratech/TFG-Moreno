using UnityEngine;
using static FlowFieldManager;

public class FlowFieldDebugger : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private bool _showInGame = false;

    [Header("Flow Field")]
    [SerializeField] private bool _showIntegration = true;
    [SerializeField] private bool _showDirections = true;

    [Header("Visual")]
    [SerializeField] private float _arrowLength = 0.4f;
    [SerializeField] private Color _arrowColor = Color.red;
    [SerializeField] private float _integrationOpacity = 0.5f;

    [Header("Providers")]
    [SerializeField] private Grid2DProvider _gridProvider;
    [SerializeField] private QuadSphereProvider _quadSphereProvider;
    [SerializeField] private Grid3DProvider _voxelProvider;

    private INavGraph _graph;

    private Grid2DNavGraph _gridGraph;
    private QuadSphereNavGraph _quadSphereGraph;
    private Grid3DNavGraph _voxelGraph;

    private Material _lineMaterial;

    private void Start()
    {
        CacheGraph();
        CreateLineMaterial();
    }

    private void CacheGraph()
    {
        if (_gridProvider != null)
        {
            _graph = _gridProvider.Graph;
            _gridGraph = _graph as Grid2DNavGraph;
            return;
        }

        if (_quadSphereProvider != null)
        {
            _graph = _quadSphereProvider.Graph;
            _quadSphereGraph = _graph as QuadSphereNavGraph;
            return;
        }

        if (_voxelProvider != null)
        {
            _graph = _voxelProvider.Graph;
            _voxelGraph = _graph as Grid3DNavGraph;
            return;
        }

        Debug.LogWarning("No Navigation Provider asignado.");
    }

    private void CreateLineMaterial()
    {
        if (_lineMaterial != null)
            return;

        Shader shader = Shader.Find("Hidden/Internal-CombinedMeshVertices");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        _lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        _lineMaterial.SetInt(
            "_SrcBlend",
            (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

        _lineMaterial.SetInt(
            "_DstBlend",
            (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        _lineMaterial.SetInt(
            "_Cull",
            (int)UnityEngine.Rendering.CullMode.Off);

        _lineMaterial.SetInt(
            "_ZWrite",
            0);
    }

    private void OnDrawGizmos()
    {
        if (_showInGame && Application.isPlaying)
            return;

        DrawAllFlows(false);
    }

    private void OnRenderObject()
    {
        if (!_showInGame)
            return;

        DrawAllFlows(true);
    }

    private void DrawAllFlows(bool inGame)
    {
        if (_graph == null)
            return;

        if (FlowFieldManager.Instance == null)
            return;

        int targetNode = FlowFieldManager.Instance.lastTargetNode;

        if (targetNode == -1)
            return;

        FlowFieldRoute route =
            FlowFieldManager.Instance.GetRoute(_graph, targetNode);

        if (route == null)
            return;

        if (inGame)
        {
            _lineMaterial.SetPass(0);
            GL.PushMatrix();
        }

        foreach (FlowField flowField in route.FlowFields.Values)
        {
            DrawFlowField(flowField, inGame);
        }

        if (inGame)
        {
            GL.PopMatrix();
        }
    }

    private void DrawFlowField(
    FlowField flowField,
    bool inGame)
    {
        if (_gridGraph != null)
        {
            DrawGridFlowField(flowField, inGame);
            return;
        }

        if (_quadSphereGraph != null)
        {
            DrawQuadSphereFlowField(flowField, inGame);
            return;
        }

        if (_voxelGraph != null)
        {
            DrawVoxelFlowField(flowField, inGame);
            return;
        }
    }

    private void DrawGridFlowField(
    FlowField flowField,
    bool inGame)
    {
        if (inGame)
        {
            if (_showIntegration)
            {
                GL.Begin(GL.QUADS);

                DrawGridIntegration(flowField);

                GL.End();
            }

            if (_showDirections)
            {
                GL.Begin(GL.LINES);

                DrawGridDirections(flowField);

                GL.End();
            }
        }
        else
        {
            DrawGridIntegrationGizmos(flowField);
            DrawGridDirectionsGizmos(flowField);
        }
    }

    private void DrawGridIntegration(
    FlowField flowField)
    {
        for (int i = 0; i < flowField.IntegrationField.Length; i++)
        {
            float cost = flowField.IntegrationField[i];

            if (cost == float.MaxValue)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            Color color =
                Color.Lerp(
                    Color.green,
                    Color.blue,
                    cost / 20f);

            color.a = _integrationOpacity;

            DrawGridQuad(
                global,
                color);
        }
    }

    private void DrawGridDirections(
    FlowField flowField)
    {
        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 dir =
                flowField.FlowDirections[i];

            if (dir == Vector3.zero)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            DrawArrowGL(
                _graph.GetNodePosition(global),
                dir);
        }
    }

    private void DrawGridIntegrationGizmos(
    FlowField flowField)
    {
        if (!_showIntegration)
            return;

        for (int i = 0; i < flowField.IntegrationField.Length; i++)
        {
            float cost = flowField.IntegrationField[i];

            if (cost == float.MaxValue)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            Color color =
                Color.Lerp(
                    Color.green,
                    Color.blue,
                    cost / 20f);

            DrawGridQuadGizmos(
                global,
                color);
        }
    }

    private void DrawGridDirectionsGizmos(
    FlowField flowField)
    {
        if (!_showDirections)
            return;

        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 dir =
                flowField.FlowDirections[i];

            if (dir == Vector3.zero)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            DrawArrowGizmos(
                _graph.GetNodePosition(global),
                dir);
        }
    }

    private void DrawGridQuad(
    int node,
    Color color)
    {
        Vector3 center = _graph.GetNodePosition(node);
        float size = _graph.GetNodeSize(node).x * 0.5f;

        GL.Color(color);

        GL.Vertex(center + new Vector3(-size, 0.01f, -size));
        GL.Vertex(center + new Vector3(-size, 0.01f, size));
        GL.Vertex(center + new Vector3(size, 0.01f, size));
        GL.Vertex(center + new Vector3(size, 0.01f, -size));
    }

    private void DrawGridQuadGizmos(
    int node,
    Color color)
    {
        Vector3 center = _graph.GetNodePosition(node);
        float size = _graph.GetNodeSize(node).x;

        Gizmos.color = color;

        Gizmos.DrawCube(
            center,
            new Vector3(size, 0.01f, size));
    }

    private void DrawArrowGL(
    Vector3 pos,
    Vector3 dir)
    {
        Vector3 end = pos + dir * _arrowLength;

        Quaternion rot =
            Quaternion.LookRotation(dir);

        Vector3 right =
            rot * Quaternion.Euler(0, 150, 0) * Vector3.forward;

        Vector3 left =
            rot * Quaternion.Euler(0, -150, 0) * Vector3.forward;

        GL.Color(_arrowColor);

        GL.Vertex(pos);
        GL.Vertex(end);

        GL.Vertex(end);
        GL.Vertex(end + right * (_arrowLength * 0.3f));

        GL.Vertex(end);
        GL.Vertex(end + left * (_arrowLength * 0.3f));
    }

    private void DrawArrowGizmos(
    Vector3 pos,
    Vector3 dir)
    {
        Vector3 end = pos + dir * _arrowLength;

        Quaternion rot =
            Quaternion.LookRotation(dir);

        Vector3 right =
            rot * Quaternion.Euler(0, 150, 0) * Vector3.forward;

        Vector3 left =
            rot * Quaternion.Euler(0, -150, 0) * Vector3.forward;

        Gizmos.color = _arrowColor;

        Gizmos.DrawLine(pos, end);

        Gizmos.DrawLine(
            end,
            end + right * (_arrowLength * 0.3f));

        Gizmos.DrawLine(
            end,
            end + left * (_arrowLength * 0.3f));
    }

    private void DrawQuadSphereFlowField(
    FlowField flowField,
    bool inGame)
    {
        if (inGame)
        {
            if (_showIntegration)
            {
                GL.Begin(GL.QUADS);

                DrawQuadSphereIntegration(flowField);

                GL.End();
            }

            if (_showDirections)
            {
                GL.Begin(GL.LINES);

                DrawQuadSphereDirections(flowField);

                GL.End();
            }
        }
        else
        {
            DrawQuadSphereIntegrationGizmos(flowField);
            DrawQuadSphereDirectionsGizmos(flowField);
        }
    }

    private void DrawQuadSphereIntegration(
    FlowField flowField)
    {
        for (int i = 0; i < flowField.IntegrationField.Length; i++)
        {
            float cost = flowField.IntegrationField[i];

            if (cost == float.MaxValue)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            Color color =
                Color.Lerp(
                    Color.green,
                    Color.blue,
                    cost / 20f);

            color.a = _integrationOpacity;

            DrawSphereQuad(
                global,
                color);
        }
    }
    private void DrawQuadSphereDirections(
    FlowField flowField)
    {
        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 dir =
                flowField.FlowDirections[i];

            if (dir == Vector3.zero)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            DrawArrowGL(
                _graph.GetNodePosition(global),
                dir);
        }
    }

    private void DrawQuadSphereIntegrationGizmos(
    FlowField flowField)
    {
        if (!_showIntegration)
            return;

        for (int i = 0; i < flowField.IntegrationField.Length; i++)
        {
            float cost = flowField.IntegrationField[i];

            if (cost == float.MaxValue)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            Color color =
                Color.Lerp(
                    Color.green,
                    Color.blue,
                    cost / 20f);

            DrawSphereQuadGizmos(
                global,
                color);
        }
    }

    private void DrawQuadSphereDirectionsGizmos(
    FlowField flowField)
    {
        if (!_showDirections)
            return;

        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 dir =
                flowField.FlowDirections[i];

            if (dir == Vector3.zero)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            DrawArrowGizmos(
                _graph.GetNodePosition(global),
                dir);
        }
    }

    private void DrawSphereQuad(
    int node,
    Color color)
    {
        CubeCoordinate coord =
            _quadSphereGraph.IndexToCoordinate(node);

        float step = 1f / _quadSphereGraph.Resolution;

        float u0 = coord.X * step;
        float u1 = (coord.X + 1) * step;

        float v0 = coord.Y * step;
        float v1 = (coord.Y + 1) * step;

        Vector3 a = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u0, v0));

        Vector3 b = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u1, v0));

        Vector3 c = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u1, v1));

        Vector3 d = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u0, v1));

        GL.Color(color);

        GL.Vertex(a);
        GL.Vertex(b);
        GL.Vertex(c);
        GL.Vertex(d);
    }

    private void DrawSphereQuadGizmos(
    int node,
    Color color)
    {
        CubeCoordinate coord =
            _quadSphereGraph.IndexToCoordinate(node);

        float step = 1f / _quadSphereGraph.Resolution;

        float u0 = coord.X * step;
        float u1 = (coord.X + 1) * step;

        float v0 = coord.Y * step;
        float v1 = (coord.Y + 1) * step;

        Vector3 a = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u0, v0));

        Vector3 b = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u1, v0));

        Vector3 c = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u1, v1));

        Vector3 d = CubeProjection.DirectionToWorld(
            _quadSphereGraph.Center,
            _quadSphereGraph.Radius,
            _quadSphereGraph.Rotation,
            CubeProjection.UVToDirection(coord.Face, u0, v1));

        Gizmos.color = color;

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    private void DrawVoxelFlowField(
    FlowField flowField,
    bool inGame)
    {
        if (inGame)
        {
            if (_showIntegration)
            {
                GL.Begin(GL.QUADS);

                DrawVoxelIntegration(flowField);

                GL.End();
            }

            if (_showDirections)
            {
                GL.Begin(GL.LINES);

                DrawVoxelDirections(flowField);

                GL.End();
            }
        }
        else
        {
            DrawVoxelIntegrationGizmos(flowField);
            DrawVoxelDirectionsGizmos(flowField);
        }
    }

    private void DrawVoxelIntegration(
    FlowField flowField)
    {
        for (int i = 0; i < flowField.IntegrationField.Length; i++)
        {
            float cost = flowField.IntegrationField[i];

            if (cost == float.MaxValue)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            Color color =
                Color.Lerp(
                    Color.green,
                    Color.blue,
                    cost / 20f);

            color.a = _integrationOpacity;

            DrawVoxelCube(global, color);
        }
    }

    private void DrawVoxelDirections(
        FlowField flowField)
    {
        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 dir = flowField.FlowDirections[i];

            if (dir == Vector3.zero)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            DrawArrowGL(
                _graph.GetNodePosition(global),
                dir);
        }
    }

    private void DrawVoxelIntegrationGizmos(
        FlowField flowField)
    {
        if (!_showIntegration)
            return;

        for (int i = 0; i < flowField.IntegrationField.Length; i++)
        {
            float cost = flowField.IntegrationField[i];

            if (cost == float.MaxValue)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            Color color =
                Color.Lerp(
                    Color.green,
                    Color.blue,
                    cost / 20f);

            DrawVoxelCubeGizmos(global, color);
        }
    }

    private void DrawVoxelDirectionsGizmos(
        FlowField flowField)
    {
        if (!_showDirections)
            return;

        for (int i = 0; i < flowField.FlowDirections.Length; i++)
        {
            Vector3 dir = flowField.FlowDirections[i];

            if (dir == Vector3.zero)
                continue;

            int global =
                _graph.GetGlobalNode(i, flowField.RegionId);

            DrawArrowGizmos(
                _graph.GetNodePosition(global),
                dir);
        }
    }

    private void DrawVoxelCube(
    int node,
    Color color)
    {
        Vector3 c = _graph.GetNodePosition(node);
        Vector3 h = _graph.GetNodeSize(node) * 0.5f;

        GL.Color(color);

        // +X
        GL.Vertex(c + new Vector3(h.x, -h.y, -h.z));
        GL.Vertex(c + new Vector3(h.x, -h.y, h.z));
        GL.Vertex(c + new Vector3(h.x, h.y, h.z));
        GL.Vertex(c + new Vector3(h.x, h.y, -h.z));

        // -X
        GL.Vertex(c + new Vector3(-h.x, -h.y, h.z));
        GL.Vertex(c + new Vector3(-h.x, -h.y, -h.z));
        GL.Vertex(c + new Vector3(-h.x, h.y, -h.z));
        GL.Vertex(c + new Vector3(-h.x, h.y, h.z));

        // +Y
        GL.Vertex(c + new Vector3(-h.x, h.y, -h.z));
        GL.Vertex(c + new Vector3(h.x, h.y, -h.z));
        GL.Vertex(c + new Vector3(h.x, h.y, h.z));
        GL.Vertex(c + new Vector3(-h.x, h.y, h.z));

        // -Y
        GL.Vertex(c + new Vector3(-h.x, -h.y, h.z));
        GL.Vertex(c + new Vector3(h.x, -h.y, h.z));
        GL.Vertex(c + new Vector3(h.x, -h.y, -h.z));
        GL.Vertex(c + new Vector3(-h.x, -h.y, -h.z));

        // +Z
        GL.Vertex(c + new Vector3(-h.x, -h.y, h.z));
        GL.Vertex(c + new Vector3(-h.x, h.y, h.z));
        GL.Vertex(c + new Vector3(h.x, h.y, h.z));
        GL.Vertex(c + new Vector3(h.x, -h.y, h.z));

        // -Z
        GL.Vertex(c + new Vector3(h.x, -h.y, -h.z));
        GL.Vertex(c + new Vector3(h.x, h.y, -h.z));
        GL.Vertex(c + new Vector3(-h.x, h.y, -h.z));
        GL.Vertex(c + new Vector3(-h.x, -h.y, -h.z));
    }

    private void DrawVoxelCubeGizmos(
        int node,
        Color color)
    {
        Vector3 center = _graph.GetNodePosition(node);
        Vector3 size = _graph.GetNodeSize(node);

        Gizmos.color = color;
        Gizmos.DrawCube(center, size);
    }
}