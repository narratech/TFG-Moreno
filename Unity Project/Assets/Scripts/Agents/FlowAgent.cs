using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    public INavGraph graph;
    public float speed = 5f;

    public Grid2DProvider grid;
    public Transform targetTransform;
    private void Start()
    {
        if (grid != null)
        {
            graph = grid.Graph;
        }
    }

    void Update()
    {
        if (graph == null) return;

        int myGlobalNode = graph.GetClosestNode(transform.position);

        int targetGlobalNode = graph.GetClosestNode(targetTransform.position);

        int myRegion = graph.GetRegionId(myGlobalNode);

        FlowField field = null;
        if (targetGlobalNode >= 0)
        {
            field = FlowFieldManager.Instance.GetFlowField(graph, myRegion, targetGlobalNode);
        }
        Debug.Log($"Agent at node {myGlobalNode} in region {myRegion} targeting node {targetGlobalNode}. Field found: {field != null}");
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