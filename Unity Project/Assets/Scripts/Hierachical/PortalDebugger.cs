using UnityEngine;
using static FlowFieldManager;

public class PortalDebugger : MonoBehaviour
{
    [Header("Providers")]
    [SerializeField] private Grid2DProvider _gridProvider;
    [SerializeField] private QuadSphereProvider _quadSphereProvider;
    [SerializeField] private Grid3DProvider _voxelProvider;

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
        if (_gridProvider != null)
        {
            _graph = _gridProvider.Graph;
            return;
        }

        if (_quadSphereProvider != null)
        {
            _graph = _quadSphereProvider.Graph;
            return;
        }

        if (_voxelProvider != null)
        {
            _graph = _voxelProvider.Graph;
            return;
        }

        _graph = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (_graph == null)
            CacheGraph();

        if (_graph == null)
            return;

        if (FlowFieldManager.Instance == null)
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