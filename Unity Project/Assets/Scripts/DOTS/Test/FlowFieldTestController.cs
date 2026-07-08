using DOTSFlowField;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Windows;

public class FlowFieldTestController : MonoBehaviour
{
    [Header("Configuración de Ventana")]
    [SerializeField] private int numRegionLevelsWindow = 2;

    public Grid2DProvider provider; // Referencia al proveedor de tu grafo clásico (INavGraph)

    private INavGraph navGraph;

    private EntityManager entityManager;

    private void Start()
    {
        // 1. Obtener el EntityManager del mundo por defecto de DOTS
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 2. Inicializar el Singleton clásico (Bridge) con los contenedores nativos
        var bridge = FlowFieldBridge.Instance;
        bridge.NumRegionLevelsWindow = numRegionLevelsWindow;
        bridge.ActiveRegionsLookup = new NativeParallelHashMap<int2, Entity>(512, Allocator.Persistent);
        bridge.GlobalPortalDistances = new NativeParallelHashMap<int, float>(1024, Allocator.Persistent);
        bridge.gridNavGraph = provider.Graph; // Asignar el grafo clásico al bridge

        navGraph = provider.Graph; // Obtener el grafo clásico desde tu proveedor

        if (navGraph == null)
        {
            Debug.LogError("[FlowFieldTestController] No se pudo obtener el grafo clásico desde el proveedor.");
            return; 
        }

        HierarchicalRouter router = null;

        if (FlowFieldManager.Instance.TryGetContext(navGraph))
        {
            FlowFieldManager.NavContext context = FlowFieldManager.Instance.GetContext(navGraph);
            router = context.Router;
        }

        if (router == null)
        {
            Debug.LogWarning("[FlowFieldTestController] No se pudo obtener el router jerárquico para el grafo clásico. Asegúrate de inicializarlo correctamente.");
        }

        NavGraphToDOTS(navGraph, router);
    }

    private void Update()
    {
        if (InputManager.Instance.IsSelecting)
        {
            Vector3 worldPos = InputManager.Instance.MouseScreenPosition;
            Ray ray = Camera.main.ScreenPointToRay(worldPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;
                int closestNode = navGraph.GetClosestNode(hitPoint);
                if (closestNode >= 0)
                {
                    // Actualizar el GlobalPortalDistances en FlowFieldBridge con la ruta calculada desde el nodo más cercano
                    RouteToDOTS(closestNode);

                    // Obtener todos los agentes y actualizar su RouteId usando EntityManager
                    var query = entityManager.CreateEntityQuery(typeof(AgentComponent));
                    using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
                    {
                        foreach (var entity in entities)
                        {
                            var agent = entityManager.GetComponentData<AgentComponent>(entity);
                            agent.RouteId = closestNode;
                            entityManager.SetComponentData(entity, agent);
                        }
                    }
                    Debug.Log($"[FlowFieldTestController] Nodo más cercano al punto de clic: {closestNode}, Posición: {navGraph.GetNodePosition(closestNode)}");
                    Debug.Log($"[FlowFieldTestController] Rutas Totatles: {FlowFieldBridge.Instance.ActiveRegionsLookup.Count()}.");
                }
                else
                {
                    Debug.LogWarning("[FlowFieldTestController] No se encontró un nodo cercano al punto de clic.");
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Súper importante para tu TFG: Limpieza estricta de memoria persistente para evitar Memory Leaks al salir del Playmode
        var bridge = FlowFieldBridge.Instance;
        if (bridge != null)
        {
            if (bridge.ActiveRegionsLookup.IsCreated) bridge.ActiveRegionsLookup.Dispose();
            if (bridge.GlobalPortalDistances.IsCreated) bridge.GlobalPortalDistances.Dispose();
        }
    }

    public void NavGraphToDOTS(INavGraph graph, HierarchicalRouter router) // Añadido el router para poder extraer los portales
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 1. Si ya existía un grafo viejo en DOTS, lo buscamos y liberamos su memoria persistente
        var query = em.CreateEntityQuery(typeof(NavGraphData));
        if (!query.IsEmpty)
        {
            var oldData = query.GetSingleton<NavGraphData>();
            oldData.Dispose(); // Evitamos Memory Leaks (Asegúrate de añadir los nuevos desatados aquí)
            em.DestroyEntity(query);
        }

        // 2. Reservamos los arrays nativos con el tamaño del grafo de C# clásico
        int nodeCount = graph.NodeCount;
        int regionCount = graph.RegionCount;

        var positions = new NativeArray<float3>(nodeCount, Allocator.Persistent);
        var costs = new NativeArray<float>(nodeCount, Allocator.Persistent);
        var walkables = new NativeArray<bool>(nodeCount, Allocator.Persistent);
        var regionIds = new NativeArray<int>(nodeCount, Allocator.Persistent);
        var globalToLocal = new NativeArray<int>(nodeCount, Allocator.Persistent);
        var localToGlobal = new NativeParallelHashMap<int2, int>(nodeCount, Allocator.Persistent);
        var regionSizes = new NativeArray<int>(regionCount, Allocator.Persistent);

        // Preparación para aplanar los vecinos
        var tempNeighborsList = new List<int>();
        var offsets = new NativeArray<int2>(nodeCount, Allocator.Persistent);

        // --- NUEVO: Listas temporales para aplanar portales ---
        var tempPortalsList = new List<int>();
        var regionPortalsOffsets = new NativeArray<int2>(regionCount, Allocator.Persistent);

        // 3. Volcado de datos nodo a nodo
        for (int i = 0; i < nodeCount; i++)
        {
            positions[i] = graph.GetNodePosition(i);
            costs[i] = graph.GetNodeCost(i);
            walkables[i] = graph.IsWalkable(i);

            int rId = graph.GetRegionId(i);
            regionIds[i] = rId;

            int localId = graph.GetLocalNode(i);
            globalToLocal[i] = localId;
            localToGlobal.TryAdd(new int2(localId, rId), i);

            // Aplanar vecinos
            int neighborStart = tempNeighborsList.Count;
            var neighbors = graph.GetNeighbors(i);
            int neighborCount = 0;
            foreach (var neighbor in neighbors)
            {
                tempNeighborsList.Add(neighbor);
                neighborCount++;
            }
            offsets[i] = new int2(neighborStart, neighborCount);
        }

        // Volcar lista de vecinos acumulada a un array nativo final
        var neighborsBuffer = new NativeArray<int>(tempNeighborsList.Count, Allocator.Persistent);
        for (int n = 0; n < tempNeighborsList.Count; n++) neighborsBuffer[n] = tempNeighborsList[n];

        // Volcar tamaños de regiones Y --- NUEVO: Extraer portales por cada región ---
        for (int r = 0; r < regionCount; r++)
        {
            regionSizes[r] = graph.GetRegionSize(r);

            // --- NUEVO: Lógica de aplanado de portales por región ---
            int portalStart = tempPortalsList.Count;

            // Obtenemos los portales de la región desde tu arquitectura clásica
            List<PortalNode> portalsInRegion = router.GetPortalsInRegion(r);
            int portalCountInRegion = 0;

            foreach (var portal in portalsInRegion)
            {
                // Identificamos cuál de los dos extremos del portal cae dentro de la región actual 'r'
                int portalGlobalNodeId = (graph.GetRegionId(portal.NodeA) == r) ? portal.NodeA : portal.NodeB;

                // Evitamos añadir el mismo nodo de portal duplicado en la misma región
                if (!tempPortalsList.Contains(portalGlobalNodeId))
                {
                    tempPortalsList.Add(portalGlobalNodeId);
                    portalCountInRegion++;
                }
            }
             
            // x = índice de inicio en el buffer plano, y = cantidad de portales que tiene la región
            regionPortalsOffsets[r] = new int2(portalStart, portalCountInRegion);
        }

        // --- NUEVO: Volcar la lista temporal de portales a su array nativo persistente ---
        var regionPortalsBuffer = new NativeArray<int>(tempPortalsList.Count, Allocator.Persistent);
        for (int p = 0; p < tempPortalsList.Count; p++) regionPortalsBuffer[p] = tempPortalsList[p];


        // 4. Guardamos todo el contenedor en una entidad Singleton de DOTS
        Entity graphEntity = em.CreateEntity();
        em.AddComponentData(graphEntity, new NavGraphData
        {
            NodeCount = nodeCount,
            RegionCount = regionCount,
            NodePositions = positions,
            NodeCosts = costs,
            IsWalkableFlags = walkables,
            NodeRegionIds = regionIds,
            GlobalToLocalMap = globalToLocal,
            LocalToGlobalMap = localToGlobal,
            NeighborsBuffer = neighborsBuffer,
            NodeNeighborsOffsets = offsets,
            RegionSizes = regionSizes,
            RegionPortalsOffsets = regionPortalsOffsets,
            RegionPortalsBuffer = regionPortalsBuffer
        });

#if UNITY_EDITOR
        em.SetName(graphEntity, "NavGraphData_Singleton");
#endif
    }

    private void RouteToDOTS(int routeId)
    {
        Debug.Log($"[FlowFieldTestController] Calculando ruta para el nodo {routeId} y actualizando GlobalPortalDistances en FlowFieldBridge.");
        // Aquí se actualiza GlobalPortalDistances en FlowFieldBridge a partir de la ruta calculada en el grafo clásico
        // Adquirir el grafo clásico y el router jerárquico
        var graph = provider.Graph;
        if (graph == null)
        {
            Debug.LogError("[FlowFieldTestController] No se pudo obtener el grafo clásico desde el proveedor.");
            return;
        }
        if (!FlowFieldManager.Instance.TryGetContext(graph))
        {
            Debug.LogError("[FlowFieldTestController] No se pudo obtener el contexto del grafo clásico.");
            return;
        }

        var context = FlowFieldManager.Instance.GetContext(graph);
        if (context == null)
        {
            Debug.LogError("[FlowFieldTestController] No se pudo obtener el contexto del grafo clásico.");
            return;
        }

        if (!context.FlowFieldCache.ContainsKey(routeId))
        {
            // Generemos la ruta si no existe en el cache
            Debug.Log($"[FlowFieldTestController] La ruta con ID {routeId} no existe en el cache. Generando nueva ruta.");
            FlowFieldManager.Instance.RegisterRoute(graph, routeId);
        }

        var route = context.FlowFieldCache[routeId];

        Debug.Log($"[FlowFieldTestController] Actualizando GlobalPortalDistances para la ruta con ID {routeId}. Portales encontrados: {route.DistanceMaps.Count}.");

        // Limpiar el GlobalPortalDistances antes de actualizarlo
        var bridge = FlowFieldBridge.Instance;
        bridge.GlobalPortalDistances.Clear();

        // Actualizar GlobalPortalDistances con los portales y sus distancias desde el nodo de destino
        foreach (var portal in route.DistanceMaps)
        {
            bridge.GlobalPortalDistances.TryAdd(portal.Key, portal.Key);
        }

    }
}