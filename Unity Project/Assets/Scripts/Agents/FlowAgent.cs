using System.Collections.Generic;
using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    public INavGraph graph;
    public float speed = 5f;
    public int targetNode = -1;
    public Grid2DProvider grid;

    private void Start()
    {
        if (grid != null)
        {
            graph = grid.Graph;
        }
    }

    void Update()
    {
        targetNode = SampleManager2.Instance.targetNode;

        if (graph == null)
        {
            Debug.LogWarning("Graph not assigned to FlowFieldAgent. Please assign a graph for the agent to navigate.");
            return;
        }

        int myGlobalNode = graph.GetClosestNode(transform.position);

        int myRegion = graph.GetRegionId(myGlobalNode);

        FlowField field = null;
        if (targetNode >= 0)
        {
            field = FlowFieldManager.Instance.GetFlowField(graph, myRegion, targetNode);
            if (field == null)
            {
                field = FlowFieldEngine.GenerateFlowPath(graph, targetNode, myGlobalNode);
            }
        }

        if (field != null)
        {
            int localIdx = graph.GetLocalNode(myGlobalNode);

            Vector3 moveDir = field.FlowDirections[localIdx];
            if (moveDir != Vector3.zero)
            {
                transform.position += moveDir * speed * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), 0.1f);
            }
        }

        if (field == null && targetNode >= 0)
        {
            Debug.LogError($"Agente en nodo global {myGlobalNode} no tiene un campo de flujo disponible para el destino {targetNode}");
        }
    }

    public void SetDestination(int globalNode)
    {
        // Aquí podrías agregar lógica adicional si quieres que el agente haga algo específico al cambiar de destino
        Debug.Log($"Destino del agente establecido a nodo global {globalNode}");
        targetNode = globalNode;

    }
}