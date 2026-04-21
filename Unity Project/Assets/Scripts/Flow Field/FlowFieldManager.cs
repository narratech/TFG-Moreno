using System.Collections.Generic;
using UnityEngine;

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
        public INavGraph NavGraph;
        public PortalGraph PortalGraph;
        public HierarchicalRouter Router;
        public RegionState[] RegionStates;
        // Caché: (RegionId, TargetNode) -> Datos del campo
        public Dictionary<(int, int), FlowField> FlowFieldCache = new Dictionary<(int, int), FlowField>();
    }

    private Dictionary<string, NavContext> _contexts = new Dictionary<string, NavContext>();

    private FlowFieldManager() { }

    public void RegisterContext(string key, INavGraph nav)
    {
        if (_contexts.ContainsKey(key))
        {
            Debug.LogWarning($"Contexto '{key}' ya registrado. Ignorando.");
            return;
        }

        if (nav == null)
        {
            Debug.LogError($"No se puede registrar un contexto con NavGraph nulo para la clave '{key}'.");
            return;
        }

        PortalGraph pg = new PortalGraph();
        PortalGraphBaker.Bake(nav, pg);

        _contexts[key] = new NavContext
        {
            NavGraph = nav,
            PortalGraph = pg,
            Router = new HierarchicalRouter(pg, nav),
            RegionStates = new RegionState[nav.RegionCount]
        };
    }

    public NavContext GetContext(string key)
    {
        if (_contexts.TryGetValue(key, out var ctx))
            return ctx;
        Debug.LogError($"Contexto '{key}' no encontrado.");
        return null;
    }
    public INavGraph GetNavGraph(string key)
    {
        if (_contexts.TryGetValue(key, out var ctx))
            return ctx.NavGraph;
        Debug.LogError($"Contexto '{key}' no encontrado.");
        return null;
    }
    public PortalGraph GetPortalGraph(string key)
    {
        if (_contexts.TryGetValue(key, out var ctx))
            return ctx.PortalGraph;
        Debug.LogError($"Contexto '{key}' no encontrado.");
        return null;
    }
    public HierarchicalRouter GetRouter(string key)
    {
        if (_contexts.TryGetValue(key, out var ctx))
            return ctx.Router;
        Debug.LogError($"Contexto '{key}' no encontrado.");
        return null;
    }

    public FlowField GetFlowField(string contextKey, int regionId, int targetNode)
    {
        if (!_contexts.TryGetValue(contextKey, out var ctx)) return null;

        var cacheKey = (regionId, targetNode);

        // 1. Si está en caché y listo, lo devolvemos
        if (ctx.FlowFieldCache.TryGetValue(cacheKey, out FlowField existingData))
        {
            if (ctx.RegionStates[regionId] == RegionState.Ready)
                return existingData;
        }

        // 2. Si no, mandamos al Obrero (Engine) a trabajar
        ctx.RegionStates[regionId] = RegionState.Calculating;

        FlowField newData = new FlowField(ctx.NavGraph.NodeCount, targetNode);

        // Llamada al Engine (el cerebro de cálculo)
        FlowFieldEngine.CalculateFlowField(ctx.NavGraph, regionId, targetNode, newData);

        ctx.FlowFieldCache[cacheKey] = newData;
        ctx.RegionStates[regionId] = RegionState.Ready;

        return newData;
    }
}