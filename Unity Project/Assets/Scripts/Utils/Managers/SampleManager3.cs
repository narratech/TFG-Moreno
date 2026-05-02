using UnityEngine;
using UnityEngine.EventSystems;

public class SampleManager3 : MonoBehaviour
{
    public Grid3DProvider graphProvider;
    public Transform targetTransform;
    private Vector3 position = Vector3.zero;
    public int targetNode = -1;

    public static SampleManager3 Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (targetTransform != null)
        {
            // Solo actualizamos el destino del flow field si el transform se ha movido significativamente
            if (Vector3.Distance(position, targetTransform.position) > 0.1f)
            {
                position = targetTransform.position;
                OnChangePlace(position);
            }
        }
    }

    private void OnChangePlace(Vector3 pos)
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
