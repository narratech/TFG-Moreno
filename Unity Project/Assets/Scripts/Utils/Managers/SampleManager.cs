using UnityEngine;
using UnityEngine.EventSystems;

public class SampleManager : MonoBehaviour
{
    public FlowFieldGrid ffgrid;

    private InputManager input;

    private bool showFlowField = false;
    private bool showAgentsPaths = false;


    void Start()
    {
        input = InputManager.Instance;
    }
    void Update()
    {
        // Si se ha hecho click y no esta sobre un boton del canvas, establecer el destino del flow field
        if (input.IsSelecting && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector3 worldPos = input.MouseScreenPosition;
            Ray ray = Camera.main.ScreenPointToRay(worldPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;
                ffgrid.SetDestination(hitPoint);
            }
        }
    }

    public void SwitchShowFF()
    {
        showFlowField = !showFlowField;
        ShowFlowField(showFlowField);
    }

    public void SwitchShowAgentsPaths()
    {
        showAgentsPaths = !showAgentsPaths;
        ShowAgentsPaths(showAgentsPaths);
    }

    public void ShowFlowField(bool show)
    {
        ffgrid.GetComponent<FlowFieldVisualizer>().showGizmos = show;
    }

    public void ShowAgentsPaths(bool show)
    {
        FlowFieldAgentVisualizer[] agents = FindObjectsByType<FlowFieldAgentVisualizer>(FindObjectsSortMode.None);
        foreach (var agent in agents)
        {
            agent.showPath = show;
        }
    }
}
