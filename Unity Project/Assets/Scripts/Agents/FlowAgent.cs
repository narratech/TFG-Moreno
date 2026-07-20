using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    [SerializeField] private MonoBehaviour provider;
    [SerializeField] private float speed = 5f;

    private INavGraph graph;
    private readonly int[] interpolationNodes = new int[8];

    private void Start()
    {
        switch (provider)
        {
            case Grid2DProvider g:
                graph = g.Graph;
                break;

            case Grid3DProvider g:
                graph = g.Graph;
                break;

            case QuadSphereProvider g:
                graph = g.Graph;
                break;
        }
    }

    private void Update()
    {
        if (graph == null)
            return;

        int targetNode = SampleManager2.Instance.targetNode;
        if (targetNode < 0)
            return;

        int myNode = graph.GetClosestNode(transform.position);
        int myRegion = graph.GetRegionId(myNode);

        FlowField field = FlowFieldManager.Instance.GetFlowField(graph, myRegion, targetNode);

        if (field == null)
            field = FlowFieldEngine.GenerateFlowPath(graph, targetNode, myNode);

        if (field == null)
            return;

        int count = graph.GetInterpolationNodes( transform.position, interpolationNodes);

        Vector3 dir = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            int node = interpolationNodes[i];

            Debug.DrawLine(
                transform.position,
                graph.GetNodePosition(node),
                Color.yellow);

            int region = graph.GetRegionId(node);

            FlowField nodeField =
                FlowFieldManager.Instance.GetFlowField(
                    graph,
                    region,
                    targetNode);

            if (nodeField == null)
                continue;

            int local = graph.GetLocalNode(node);

            dir += nodeField.FlowDirections[local];
        }

        if (dir == Vector3.zero)
            return;

        dir.Normalize();

        transform.position += dir * speed * Time.deltaTime;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            10f * Time.deltaTime);
    }
}