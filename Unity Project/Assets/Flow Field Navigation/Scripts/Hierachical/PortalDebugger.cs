using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class PortalDebugger : MonoBehaviour
{
    [Header("Provider")]
    [SerializeField] private NavGraphProvider _provider;

    [Header("Visual")]
    [SerializeField] private Color _portalColor = Color.blue;
    [SerializeField] private Color _connectionColor = Color.magenta;
    [SerializeField] private float _sphereRadius = 0.5f;
    [SerializeField] private float _lineWidth = 6.0f;
    [SerializeField] private bool _drawConnections = true;

    private INavGraph _graph;

    private void Awake() => CacheGraph();

#if UNITY_EDITOR
    private void OnEnable()
    {
        // Enganchar el dibujado al pase final de la SceneView
        SceneView.duringSceneGui += RenderOnTop;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= RenderOnTop;
    }

    private void OnValidate() => CacheGraph();
#endif

    private void CacheGraph()
    {
        if (_provider != null)
            _graph = _provider.Graph;
    }

    private void RenderOnTop(SceneView sceneView)
    {
        // Solo dibuja si el objeto est� seleccionado (simula OnDrawGizmosSelected)
        if (Selection.activeGameObject != gameObject) return;

        if (_graph == null) CacheGraph();
        if (_graph == null || FlowFieldManager.Instance == null) return;
        if (!FlowFieldManager.Instance.TryGetContext(_graph)) return;

        FlowFieldManager.NavContext ctx = FlowFieldManager.Instance.GetContext(_graph);

        // Desactivar el Z-Buffer a nivel de gráficos de Handles
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        Handles.color = _connectionColor;
        foreach (var portal in ctx.PortalGraph.GetAllPortals())
        {
            Vector3 middle = (portal.PositionA + portal.PositionB) * 0.5f;
            if (_drawConnections)
            {
                foreach (var edge in ctx.PortalGraph.GetNeighbors(portal.Id))
                {
                    Vector3 middleNeighbour = (ctx.PortalGraph.GetPortal(edge.TargetPortalId).PositionA + ctx.PortalGraph.GetPortal(edge.TargetPortalId).PositionB) * 0.5f;

                    // L�nea con grosor real por encima de la escena y otros gizmos
                    Handles.DrawAAPolyLine(_lineWidth, middle, middleNeighbour);
                }
            }
        }
        
        Handles.color = _portalColor;
        foreach (var portal in ctx.PortalGraph.GetAllPortals())
        {
            Vector3 middle = (portal.PositionA + portal.PositionB) * 0.5f;

            Vector3 camForward = sceneView.camera.transform.forward;
            Handles.DrawSolidDisc(middle, camForward, _sphereRadius);
        }
    }
}