using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestor centralizado para manejar múltiples contextos de navegación y sus respectivos campos de flujo.
/// </summary>
public class FlowFieldManager
{
    private static FlowFieldManager _instance;
    public static FlowFieldManager Instance => _instance ??= new FlowFieldManager();

    public enum RegionState { Uninitialized, Calculating, Ready, Dirty }

    public class NavContext
    {
        public PortalGraph PortalGraph;
        public HierarchicalRouter Router;
        public RegionState[] RegionStates;
        // Caché: (RegionId, TargetNode) -> Datos del campo
        public Dictionary<(int, int), FlowField> FlowFieldCache = new Dictionary<(int, int), FlowField>();
    }

    private Dictionary<INavGraph, NavContext> _contexts = new Dictionary<INavGraph, NavContext>();

    private FlowFieldManager() { }

    public void RegisterContext(INavGraph nav)
    {
        if (nav == null)
        {
            Debug.LogError($"No se puede registrar un contexto con NavGraph nulo.");
            return;
        }

        if (_contexts.ContainsKey(nav))
        {
            Debug.LogWarning($"Contexto ya registrado. Ignorando.");
            return;
        }

        PortalGraph pg = new PortalGraph();
        PortalGraphBaker.Bake(nav, pg);

        _contexts[nav] = new NavContext
        {
            PortalGraph = pg,
            Router = new HierarchicalRouter(pg, nav),
            RegionStates = new RegionState[nav.RegionCount]
        };
    }

    public NavContext GetContext(INavGraph nav)
    {
        if (_contexts.TryGetValue(nav, out var ctx))
            return ctx;
        Debug.LogError($"Contexto '{nav}' no encontrado.");
        return null;
    }
    public PortalGraph GetPortalGraph(INavGraph nav)
    {
        if (_contexts.TryGetValue(nav, out var ctx))
            return ctx.PortalGraph;
        Debug.LogError($"Contexto '{nav}' no encontrado.");
        return null;
    }
    public HierarchicalRouter GetRouter(INavGraph nav)
    {
        if (_contexts.TryGetValue(nav, out var ctx))
            return ctx.Router;
        Debug.LogError($"Contexto '{nav}' no encontrado.");
        return null;
    }
    public Dictionary<(int, int), FlowField> GetFlowFieldCache(INavGraph nav)
    {
        if (_contexts.TryGetValue(nav, out var ctx))
            return ctx.FlowFieldCache;
        Debug.LogError($"Contexto '{nav}' no encontrado.");
        return null;
    }

    public bool TryGetContext(INavGraph nav)
    {
        return _contexts.TryGetValue(nav, out var ctx);
    }

    public FlowField GetFlowField(INavGraph nav, int regionId, int targetNode)
    {
        if (!_contexts.TryGetValue(nav, out var ctx)) return null;

        var cacheKey = (regionId, targetNode);

        // 1. Si está en caché y listo, lo devolvemos
        if (ctx.FlowFieldCache.TryGetValue(cacheKey, out FlowField existingData))
        {
            if (ctx.RegionStates[regionId] == RegionState.Ready)
                return existingData;
        }

        // 2. Si no, mandamos al Obrero (Engine) a trabajar
        ctx.RegionStates[regionId] = RegionState.Calculating;

        // Llamada al Engine (el cerebro de cálculo)
        
        FlowFieldEngine.CalculateFlowField(nav, regionId, targetNode);
        ctx.RegionStates[regionId] = RegionState.Ready;

        return ctx.FlowFieldCache[cacheKey];
    }
}