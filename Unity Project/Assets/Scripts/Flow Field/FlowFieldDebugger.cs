using UnityEngine;

public class FlowFieldDebugger : MonoBehaviour
{
    [Header("Configuración Visual")]
    [SerializeField] private bool _showIntegration = false;
    [SerializeField] private bool _showDirections = true;
    [SerializeField] private float _arrowLength = 0.4f;
    [SerializeField] private Color _arrowColor = Color.yellow;

    private void Start()
    {
        // Aseguramos que el Singleton esté inicializado
        if (FlowFieldManager.Instance == null)
        {
            Debug.LogWarning("FlowFieldManager no está inicializado. Asegúrate de que exista en la escena.");
        }
    }

    private void OnDrawGizmos()
    {
        // Accedemos a través del Singleton que creaste
        if (FlowFieldManager.Instance == null) return;

        var data = FlowFieldManager.Instance.LastCalculatedData;
        var graph = FlowFieldManager.Instance.LastUsedGraph;

        if (data == null || graph == null) return;

        for (int i = 0; i < graph.NodeCount; i++)
        {
            Vector3 pos = graph.GetNodePosition(i);

            // 1. Dibujar el mapa de calor (Integration Field)
            if (_showIntegration)
            {
                float cost = data.IntegrationField[i];
                if (cost < float.MaxValue)
                {
                    // Degradado de verde (cerca) a rojo (lejos)
                    Gizmos.color = Color.Lerp(Color.green, Color.red, cost / 50f);
                    Gizmos.DrawCube(pos, new Vector3(0.8f, 0.05f, 0.8f));
                }
            }

            // 2. Dibujar las flechas (Directions)
            if (_showDirections)
            {
                Vector3 dir = data.FlowDirections[i];
                if (dir != Vector3.zero)
                {
                    DrawArrow(pos, dir);
                }
            }
        }
    }

    private void DrawArrow(Vector3 pos, Vector3 dir)
    {
        Gizmos.color = _arrowColor;
        Vector3 end = pos + dir * _arrowLength;

        // Línea de la flecha
        Gizmos.DrawLine(pos, end);

        // Cabeza de la flecha
        Vector3 right = Quaternion.LookRotation(dir != Vector3.zero ? dir : Vector3.forward) * Quaternion.Euler(0, 150, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(dir != Vector3.zero ? dir : Vector3.forward) * Quaternion.Euler(0, -150, 0) * Vector3.forward;

        Gizmos.DrawLine(end, end + right * (_arrowLength * 0.3f));
        Gizmos.DrawLine(end, end + left * (_arrowLength * 0.3f));
    }
}