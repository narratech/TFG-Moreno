using UnityEngine;

public class PortalDebuger : MonoBehaviour
{
    public Grid2DProvider graphProvider;
    private INavGraph _navGraph;
    private PortalGraph _portalGraph;
    private bool _isBaked = false;

    // Esto solo funcionará en Play Mode
    private void Start()
    {
        EnsureBake();
    }

    private void EnsureBake()
    {
        if (_isBaked) return;

        // Intentamos obtener el grafo
        if (graphProvider != null)
        {
            graphProvider.InitializeGraph();
        }
        else
        {
            return;
        }

        _navGraph = graphProvider.Graph;

        if (_navGraph != null)
        {
            _portalGraph = new PortalGraph();
            PortalGraphBaker.Bake(_navGraph, _portalGraph);
            _isBaked = true;
        }
    }

    private void OnDrawGizmos()
    {
        // En lugar de esperar al Start, lo intentamos aquí cada vez que sea null
        if (!_isBaked)
        {
            EnsureBake();
        }

        if (_navGraph == null)
        {
            Debug.LogWarning("PortalDebuger: No se pudo obtener el NavGraph.");
            return;
        }

        if (_portalGraph == null)
        {
            Debug.LogWarning("PortalDebuger: No se pudo obtener el PortalGraph.");
            return;
        }

        Gizmos.color = Color.magenta;
        foreach (var portal in _portalGraph.GetAllPortals())
        {
            // Usamos las posiciones que ya tiene el portal (si las guardaste en el Bake)
            // o las pedimos al navGraph
            Vector3 posA = portal.PositionA;
            Vector3 posB = portal.PositionB;

            Gizmos.DrawLine(posA, posB);
            Gizmos.DrawWireSphere(posA, 0.5f);
            Gizmos.DrawWireSphere(posB, 0.5f);
        }
    }
}