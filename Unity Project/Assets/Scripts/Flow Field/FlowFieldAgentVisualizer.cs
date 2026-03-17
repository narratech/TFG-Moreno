using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FlowFieldAgent))]
[RequireComponent(typeof(LineRenderer))]
public class FlowFieldAgentVisualizer : MonoBehaviour
{
    public float minDistance = 0.2f; // distancia mínima entre puntos para dibujar
    public Color pathColor = Color.cyan;
    public float lineWidth = 0.1f;
    public bool showPath = true;

    private LineRenderer lr;
    private List<Vector3> points = new List<Vector3>();
    private FlowFieldAgent agent;

    void Awake()
    {
        agent = GetComponent<FlowFieldAgent>();
        lr = GetComponent<LineRenderer>();

        // Configuración visual de la línea
        lr.positionCount = 0;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = pathColor;
        lr.endColor = pathColor;
    }

    void Update()
    {
        if (!showPath) return;

        Vector3 currentPos = transform.position;

        if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], currentPos) > minDistance)
        {
            points.Add(currentPos);
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
        }
    }

    // Si quieres reiniciar el camino en cualquier momento
    public void ClearPath()
    {
        points.Clear();
        lr.positionCount = 0;
    }
}
