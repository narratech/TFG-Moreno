using UnityEngine;
using static FlowFieldManager;

public class PortalDebugger : MonoBehaviour
{
    [Header("Provider")]
    [SerializeField] private NavGraphProvider _provider;

    [Header("Visual")]
    [SerializeField] private Color _color = Color.magenta;
    [SerializeField] private float _sphereRadius = 0.5f;
    [SerializeField] private bool _drawConnections = true;

    private INavGraph _graph;

    private void Awake()
    {
        CacheGraph();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheGraph();
    }
#endif

    private void CacheGraph()
    {
        _graph = _provider.Graph;
    }

    private void OnDrawGizmosSelected()
    {
        if (_graph == null)
            CacheGraph();

        if (_graph == null || FlowFieldManager.Instance == null)
            return;

        if (!FlowFieldManager.Instance.TryGetContext(_graph))
            return;

        NavContext ctx = FlowFieldManager.Instance.GetContext(_graph);

        Gizmos.color = _color;

        foreach (var portal in ctx.PortalGraph.GetAllPortals())
        {
            Vector3 middle = (portal.PositionA + portal.PositionB) * 0.5f;

            Gizmos.DrawSphere(middle, _sphereRadius);

            if (_drawConnections)
            {
                Gizmos.DrawLine(portal.PositionA, portal.PositionB);
            }
        }
    }
}