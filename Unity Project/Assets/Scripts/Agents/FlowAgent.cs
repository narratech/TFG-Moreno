using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    public INavGraph graph;
    public int targetNode;
    public float speed = 5f;

    private void Awake()
    {

    }

    void Update()
    {
        if (graph == null) return;

        int myGlobalNode = graph.GetClosestNode(transform.position);

        int myRegion = graph.GetRegionId(myGlobalNode);

        FlowField field = null;
        if (targetNode >= 0)
        {
            field = FlowFieldManager.Instance.GetFlowField(graph, myRegion, targetNode);
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
    }
}