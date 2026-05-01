using UnityEngine;
using UnityEngine.EventSystems;

public class SampleManager2 : MonoBehaviour
{
    public Grid2DProvider graphProvider;
    public int targetNode = -1;
    private InputManager input;

    // patron singleton de SampleManager2
    public static SampleManager2 Instance { get; private set; }
    private void Awake()
    {
        // Configurar singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }


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
                OnClickGround(hitPoint);
            }
        }
    }

    private void OnClickGround(Vector3 pos)
    {
        int destination = graphProvider.Graph.GetClosestNode(pos);
        if (destination == -1)
        {
            targetNode = -1;
            return;
        }

        FlowFieldManager.Instance.RegisterRoute(graphProvider.Graph, destination);
        targetNode = destination;
    }
}
