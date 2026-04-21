using UnityEngine;
using UnityEngine.EventSystems;

public class SampleManager2 : MonoBehaviour
{
    public Grid2DProvider graphProvider;

    private InputManager input;

    void Start()
    {
        input = InputManager.Instance;
    }
    void Update()
    {
        // Si se ha hecho click y no esta sobre un boton del canvas, establecer el destino del flow field
        if (input.IsSelecting)
        {
            Vector3 worldPos = input.MouseScreenPosition;
            Ray ray = Camera.main.ScreenPointToRay(worldPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log("Hit point: " + hit.point);
                Vector3 hitPoint = hit.point;
                //FlowFieldData data = new FlowFieldData(graphProvider.Graph.NodeCount, graphProvider.Graph.GetClosestNode(hitPoint));
                //FlowFieldManager.Instance.CalculateFlowField(graphProvider.Graph, data);

            }
        }
    }
}
