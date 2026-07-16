using UnityEngine;

public class QuadSphereProvider : MonoBehaviour
{
    [Header("Configuracion de QuadSphere")]
    [SerializeField] private float _radius = 10f;
    [SerializeField] private int _resolution = 32;
    [SerializeField] private int _regionsPerAxis = 4;

    [Header("Obstacles")]
    [SerializeField] private LayerMask _obstacleMask;

    public QuadSphereNavGraph Graph { get; private set; }

    private void Awake()
    {
        InitializeGraph();
    }

    public void InitializeGraph()
    {
        if (Graph != null)
            return;

        Graph = new QuadSphereNavGraph(
            transform.position,
            _radius,
            transform.rotation,
            _resolution,
            _regionsPerAxis);

        ScanObstacles();

        FlowFieldManager.Instance.RegisterContext(Graph);
    }

    private void ScanObstacles()
    {
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 position = Graph.GetNodePosition(i);

            float nodeRadius = Graph.GetNodeSize(i).x * 0.45f;

            bool blocked = Physics.CheckSphere(
                position,
                nodeRadius,
                _obstacleMask);

            Graph.SetWalkable(i, !blocked);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(
            transform.position,
            _radius);

        if (Graph == null)
            return;

        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Gizmos.color =
                Graph.IsWalkable(i)
                ? new Color(0f, 1f, 1f, 0.25f)
                : new Color(1f, 0f, 0f, 0.5f);

            Gizmos.DrawSphere(
                Graph.GetNodePosition(i),
                Graph.GetNodeSize(i).x * 0.25f);
        }
    }
}