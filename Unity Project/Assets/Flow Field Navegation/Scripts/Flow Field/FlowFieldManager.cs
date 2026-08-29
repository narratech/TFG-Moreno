using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Gestor centralizado para manejar múltiples contextos de navegación y sus respectivos campos de flujo.
/// </summary>
public class FlowFieldManager
{
    private static FlowFieldManager _instance;
    public static FlowFieldManager Instance => _instance ??= new FlowFieldManager();

    public int lastTargetNode = -1;

    public enum RegionState { Uninitialized, Calculating, Ready, Dirty }

    public class FlowFieldRoute
    {
        public Dictionary<int, float> DistanceMaps; // PortalId -> Distancia al destino
        public Dictionary<int, FlowField> FlowFields; // RegionId -> FlowField

        public FlowFieldRoute()
        {
            DistanceMaps = new Dictionary<int, float>();
            FlowFields = new Dictionary<int, FlowField>();
        }
    }

    public class NavContext
    {
        public PortalGraph PortalGraph;
        public HierarchicalRouter Router;

        // Cache de rutas de flujo: TargetNode -> FlowFieldRoute
        public Dictionary<int, FlowFieldRoute> FlowFieldCache;

        public NavContext()
        {
            FlowFieldCache = new Dictionary<int, FlowFieldRoute>();
        }
    }

    private Dictionary<INavGraph, NavContext> _contexts = new Dictionary<INavGraph, NavContext>();

    private Dictionary<int, INavGraph> _graphIds = new Dictionary<int, INavGraph>();
    private FlowFieldManager() { }

    public INavGraph GetNavGraphById(int graphId)
    {
        if (_graphIds.TryGetValue(graphId, out var navGraph))
            return navGraph;
        Debug.LogError($"NavGraph con ID {graphId} no encontrado.");
        return null;
    }

    public void RegisterContext(INavGraph nav)
    {
        if (nav == null)
        {
            Debug.LogError("No se puede registrar un contexto con NavGraph nulo.");
            return;
        }

        if (_contexts.ContainsKey(nav))
        {
            Debug.LogWarning("Contexto ya registrado. Ignorando.");
            return;
        }

        // Asignar ID único
        nav.GraphId = _contexts.Count;

        // Construir representación nativa para DOTS
        NavGraphData graphData = NavGraphFactory.CreateNavGraphData(nav);

        // Registrar el grafo en el almacenamiento DOTS
        FlowFieldStorage.Instance.RegisterNavGraphData(graphData);

        PortalGraph pg = new PortalGraph();
        PortalGraphBaker.Bake(nav, pg);

        _contexts[nav] = new NavContext
        {
            PortalGraph = pg,
            Router = new HierarchicalRouter(pg, nav)
        };

        _graphIds.Add(nav.GraphId, nav);
    }

    public NavContext GetContext(INavGraph nav)
    {
        if (_contexts.TryGetValue(nav, out var ctx))
            return ctx;
        Debug.LogError($"Contexto '{nav}' no encontrado.");
        return null;
    }

    public bool TryGetContext(INavGraph nav)
    {
        return _contexts.TryGetValue(nav, out var ctx);
    }

    public void RegisterRoute(INavGraph nav, int targetNode)
    {
        if (!_contexts.TryGetValue(nav, out var ctx))
        {
            Debug.LogError($"No se puede registrar ruta. Contexto '{nav}' no encontrado.");
            return;
        }
    
        if (ctx.FlowFieldCache.ContainsKey(targetNode))
        {
            Debug.LogWarning($"Ruta para TargetNode {targetNode} ya registrada. Ignorando.");
            return;
        }

        ctx.FlowFieldCache[targetNode] = new FlowFieldRoute
        {
            DistanceMaps = ctx.Router.GetPortalDistanceField(targetNode),
            FlowFields = new Dictionary<int, FlowField>()
        };

        lastTargetNode = targetNode;
    }

    public FlowFieldRoute GetRoute(INavGraph nav, int targetNode)
    {
        if (!_contexts.TryGetValue(nav, out var ctx))
        {
            Debug.LogError($"No se puede obtener ruta. Contexto '{nav}' no encontrado.");
            return null;
        }
        if (ctx.FlowFieldCache.TryGetValue(targetNode, out var route))
            return route;
        Debug.LogWarning($"Ruta para TargetNode {targetNode} no encontrada.");
        return null;
    }

    public bool TryGetRoute(INavGraph nav, int targetNode)
    {
        return _contexts.TryGetValue(nav, out var ctx) && ctx.FlowFieldCache.ContainsKey(targetNode);
    }

    public FlowField GetFlowField(INavGraph nav, int regionId, int targetNode)
    {
        if (!_contexts.TryGetValue(nav, out var ctx))
        {
            Debug.LogError($"No se puede obtener FlowField. Contexto '{nav}' no encontrado.");
            return null;
        }

        if (ctx.FlowFieldCache.TryGetValue(targetNode, out FlowFieldRoute existingData))
        {
            if (existingData.FlowFields.TryGetValue(regionId, out var cachedField))
            {
                return cachedField;
            }
        }

        return null;
    }

    public void EliminateRoute(INavGraph nav, int targetNode)
    {
        if (!_contexts.TryGetValue(nav, out var ctx))
        {
            Debug.LogError($"No se puede eliminar ruta. Contexto '{nav}' no encontrado.");
            return;
        }

        if (!ctx.FlowFieldCache.Remove(targetNode))
        {
            Debug.LogWarning($"Ruta para TargetNode {targetNode} no encontrada.");
            return;
        }

        if (lastTargetNode == targetNode)
            lastTargetNode = -1;
    }

    public void RequestFlowField(
    int graphId,
    int routeId,
    int regionId)
    {
        INavGraph graph = GetNavGraphById(graphId);

        if (graph == null)
            return;

        // routeId = targetNode en tu arquitectura actual
        int targetNode = routeId;

        FlowField field = GetFlowField(graph, regionId, targetNode);

        // Ya existe.
        if (field != null)
            return;

        field = FlowFieldEngine.GenerateFlowPath(graph, targetNode, regionId);

        if (field == null)
        {
            Debug.LogWarning(
                $"No se pudo generar FlowField: " +
                $"Graph={graphId}, " +
                $"Route={routeId}, " +
                $"Region={regionId}");

            return;
        }

        if (!_contexts.TryGetValue(graph, out var ctx))
            return;

        if (!ctx.FlowFieldCache.TryGetValue(
                targetNode,
                out var route))
            return;

        route.FlowFields[regionId] = field;

        Debug.Log($"FlowField solicitado: Graph={graphId}, Route={routeId}, Region={regionId}");
    }
}