using UnityEngine;

public class FlowFieldDebugger : MonoBehaviour
{
    [Header("Configuración Visual")]
    [SerializeField] private bool _showInGame = false;
    [SerializeField] private bool _showIntegration = false;
    [SerializeField] private bool _showDirections = true;
    [SerializeField] private float _arrowLength = 0.4f;
    [SerializeField] private Color _arrowColor = Color.red;
    [SerializeField][Range(0f, 1f)] private float _planeOpacity = 0.5f;

    private Material _lineMaterial;

    private void Start()
    {
        if (FlowFieldManager.Instance == null)
            Debug.LogWarning("FlowFieldManager no encontrado.");

        CreateLineMaterial();
    }

    private void OnRenderObject()
    {
        if (!_showInGame) return;
        DrawFlowFieldInternal(true);
    }

    private void OnDrawGizmos()
    {
        if (_showInGame && Application.isPlaying) return;
        DrawFlowFieldInternal(false);
    }

    private void DrawFlowFieldInternal(bool isInGame)
    {
        if (FlowFieldManager.Instance == null) return;

        var data = FlowFieldManager.Instance.LastCalculatedData;
        var graph = FlowFieldManager.Instance.LastUsedGraph;

        if (data == null || graph == null) return;

        if (isInGame)
        {
            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            // Dibujamos primero los planos (QUADS)
            if (_showIntegration)
            {
                GL.Begin(GL.QUADS);
                for (int i = 0; i < graph.NodeCount; i++)
                {
                    float cost = data.IntegrationField[i];
                    if (cost < float.MaxValue)
                    {
                        Color c = Color.Lerp(Color.green, Color.blue, cost / 20f);
                        c.a = _planeOpacity;
                        DrawInGamePlane(graph.GetNodePosition(i), graph.GetNodeSize(i).x, c);
                    }
                }
                GL.End();
            }

            // Luego las flechas (LINES)
            if (_showDirections)
            {
                GL.Begin(GL.LINES);
                for (int i = 0; i < graph.NodeCount; i++)
                {
                    Vector3 dir = data.FlowDirections[i];
                    if (dir != Vector3.zero) DrawArrow(graph.GetNodePosition(i), dir, true);
                }
                GL.End();
            }
            GL.PopMatrix();
        }
        else
        {
            // Lógica normal de Gizmos para el Editor
            for (int i = 0; i < graph.NodeCount; i++)
            {
                Vector3 pos = graph.GetNodePosition(i);
                if (_showIntegration)
                {
                    float cost = data.IntegrationField[i];
                    if (cost < float.MaxValue)
                    {
                        Gizmos.color = Color.Lerp(Color.green, Color.blue, cost / 50f);
                        Gizmos.DrawCube(pos, new Vector3(0.9f, 0.01f, 0.9f));
                    }
                }
                if (_showDirections)
                {
                    Vector3 dir = data.FlowDirections[i];
                    if (dir != Vector3.zero) DrawArrow(pos, dir, false);
                }
            }
        }
    }

    private void DrawInGamePlane(Vector3 center, float size, Color color)
    {
        GL.Color(color);
        float h = size * 0.5f;
        // Dibujamos el plano horizontalmente (XZ)
        GL.Vertex(new Vector3(center.x - h, center.y + 0.01f, center.z - h));
        GL.Vertex(new Vector3(center.x - h, center.y + 0.01f, center.z + h));
        GL.Vertex(new Vector3(center.x + h, center.y + 0.01f, center.z + h));
        GL.Vertex(new Vector3(center.x + h, center.y + 0.01f, center.z - h));
    }

    private void DrawArrow(Vector3 pos, Vector3 dir, bool isInGame)
    {
        Vector3 end = pos + dir * _arrowLength;
        Quaternion lookRot = Quaternion.LookRotation(dir != Vector3.zero ? dir : Vector3.forward);
        Vector3 right = lookRot * Quaternion.Euler(0, 150, 0) * Vector3.forward;
        Vector3 left = lookRot * Quaternion.Euler(0, -150, 0) * Vector3.forward;

        if (isInGame)
        {
            GL.Color(_arrowColor);
            GL.Vertex(pos); GL.Vertex(end);
            GL.Vertex(end); GL.Vertex(end + right * (_arrowLength * 0.3f));
            GL.Vertex(end); GL.Vertex(end + left * (_arrowLength * 0.3f));
        }
        else
        {
            Gizmos.color = _arrowColor;
            Gizmos.DrawLine(pos, end);
            Gizmos.DrawLine(end, end + right * (_arrowLength * 0.3f));
            Gizmos.DrawLine(end, end + left * (_arrowLength * 0.3f));
        }
    }

    private void CreateLineMaterial()
    {
        if (_lineMaterial != null) return;

        // Intentamos buscar el shader más simple posible
        Shader shader = Shader.Find("Hidden/Internal-CombinedMeshVertices");

        // Si sigue siendo null, buscamos el de Sprites (funciona en Built-in y URP para GL)
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        // Si todo falla (extremo), usamos el shader de error para que al menos no rompa
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        _lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        // Configuración para que soporte transparencia y colores de vértice
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);
    }
}