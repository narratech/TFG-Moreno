//using DOTSFlowField;
//using System.Collections.Generic;
//using System.Linq;
//using Unity.Collections;
//using Unity.Entities;
//using Unity.Mathematics;
//using UnityEngine;
//using UnityEngine.Windows;

//public class FlowFieldTestController : MonoBehaviour
//{
//    [Header("Configuración de Ventana")]
//    [SerializeField] private int numRegionLevelsWindow = 2;

//    public NavGraphProvider provider; // Referencia al proveedor de tu grafo clásico (INavGraph)

//    private INavGraph navGraph;

//    private EntityManager entityManager;

//    private void Start()
//    {
//        // 1. Obtener el EntityManager del mundo por defecto de DOTS
//        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

//        // 2. Inicializar el Singleton clásico (Bridge) con los contenedores nativos
//        var bridge = FlowFieldBridge.Instance;
//        bridge.NumRegionLevelsWindow = numRegionLevelsWindow;
//        bridge.ActiveRegionsLookup = new NativeParallelHashMap<int2, Entity>(512, Allocator.Persistent);
//        bridge.GlobalPortalDistances = new NativeParallelHashMap<int, float>(1024, Allocator.Persistent);
//        bridge.gridNavGraph = provider.Graph; // Asignar el grafo clásico al bridge

//        navGraph = provider.Graph; // Obtener el grafo clásico desde tu proveedor

//        bridge.PhasesMap = new NativeParallelHashMap<int, int>(navGraph.RegionCount, Allocator.Persistent);

//        if (navGraph == null)
//        {
//            Debug.LogError("[FlowFieldTestController] No se pudo obtener el grafo clásico desde el proveedor.");
//            return; 
//        }

//        HierarchicalRouter router = null;
//        PortalGraph portalGraph = null;

//        if (FlowFieldManager.Instance.TryGetContext(navGraph))
//        {
//            FlowFieldManager.NavContext context = FlowFieldManager.Instance.GetContext(navGraph);
//            router = context.Router;
//            portalGraph = context.PortalGraph;
//        }

//        if (router == null)
//        {
//            Debug.LogWarning("[FlowFieldTestController] No se pudo obtener el router jerárquico para el grafo clásico. Asegúrate de inicializarlo correctamente.");
//        }

//        NavGraphToDOTS(navGraph, portalGraph);
//    }

//    private void Update()
//    {
//        if (InputManager.Instance.IsSelecting)
//        {
//            Vector3 worldPos = InputManager.Instance.MouseScreenPosition;
//            Ray ray = Camera.main.ScreenPointToRay(worldPos);
//            if (Physics.Raycast(ray, out RaycastHit hit))
//            {
//                Vector3 hitPoint = hit.point;
//                int closestNode = navGraph.GetClosestNode(hitPoint);
//                if (closestNode >= 0)
//                {
//                    // Actualizar el GlobalPortalDistances en FlowFieldBridge con la ruta calculada desde el nodo más cercano
//                    RouteToDOTS(closestNode);

//                    // Obtener todos los agentes y actualizar su RouteId usando EntityManager
//                    var query = entityManager.CreateEntityQuery(typeof(AgentComponent));
//                    using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
//                    {
//                        foreach (var entity in entities)
//                        {
//                            var agent = entityManager.GetComponentData<AgentComponent>(entity);
//                            agent.RouteId = closestNode;
//                            entityManager.SetComponentData(entity, agent);
//                        }
//                    }
//                    Debug.Log($"[FlowFieldTestController] Nodo más cercano al punto de clic: {closestNode}, Posición: {navGraph.GetNodePosition(closestNode)}");
//                }
//                else
//                {
//                    Debug.LogWarning("[FlowFieldTestController] No se encontró un nodo cercano al punto de clic.");
//                }
//            }
//        }
//    }

//    private void OnDestroy()
//    {
//        // Súper importante para tu TFG: Limpieza estricta de memoria persistente para evitar Memory Leaks al salir del Playmode
//        var bridge = FlowFieldBridge.Instance;
//        if (bridge != null)
//        {
//            if (bridge.ActiveRegionsLookup.IsCreated) bridge.ActiveRegionsLookup.Dispose();
//            if (bridge.GlobalPortalDistances.IsCreated) bridge.GlobalPortalDistances.Dispose();
//            if (bridge.PhasesMap.IsCreated) bridge.PhasesMap.Dispose();
//        }
//    }

//    public void NavGraphToDOTS(INavGraph graph, PortalGraph portalGraph)
//    {
//        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

//        // 1. Limpieza de memoria vieja
//        var query = em.CreateEntityQuery(typeof(NavGraphData));
//        if (!query.IsEmpty)
//        {
//            var oldData = query.GetSingleton<NavGraphData>();
//            oldData.Dispose();
//            em.DestroyEntity(query);
//        }

//        int nodeCount = graph.NodeCount;
//        int regionCount = graph.RegionCount;

//        // 2. Inicialización de arrays nativos estándar
//        var positions = new NativeArray<float3>(nodeCount, Allocator.Persistent);
//        var costs = new NativeArray<float>(nodeCount, Allocator.Persistent);
//        var walkables = new NativeArray<bool>(nodeCount, Allocator.Persistent);
//        var regionIds = new NativeArray<int>(nodeCount, Allocator.Persistent);
//        var globalToLocal = new NativeArray<int>(nodeCount, Allocator.Persistent);
//        var localToGlobal = new NativeParallelHashMap<int2, int>(nodeCount, Allocator.Persistent);
//        var regionSizes = new NativeArray<int>(regionCount, Allocator.Persistent);

//        var tempNeighborsList = new List<int>();
//        var nodeNeighborsOffsets = new NativeParallelHashMap<int, int2>(nodeCount, Allocator.Persistent);

//        // Listas de portales aplanadas por región
//        var tempPortalsList = new List<int>();
//        var regionPortalsOffsets = new NativeArray<int2>(regionCount, Allocator.Persistent);

//        // 3. Volcado de datos nodo a nodo
//        for (int i = 0; i < nodeCount; i++)
//        {
//            positions[i] = graph.GetNodePosition(i);
//            costs[i] = graph.GetNodeCost(i);
//            walkables[i] = graph.IsWalkable(i);

//            int rId = graph.GetRegionId(i);
//            regionIds[i] = rId;

//            int localId = graph.GetLocalNode(i);
//            globalToLocal[i] = localId;
//            localToGlobal.TryAdd(new int2(localId, rId), i);

//            int neighborStart = tempNeighborsList.Count;
//            var neighbors = graph.GetNeighbors(i);
//            int neighborCount = 0;
//            foreach (var neighbor in neighbors)
//            {
//                tempNeighborsList.Add(neighbor);
//                neighborCount++;
//            }
//            nodeNeighborsOffsets.TryAdd(i, new int2(neighborStart, neighborCount));
//        }

//        var neighborsBuffer = new NativeArray<int>(tempNeighborsList.Count, Allocator.Persistent);
//        for (int n = 0; n < tempNeighborsList.Count; n++) neighborsBuffer[n] = tempNeighborsList[n];

//        // 4. Volcado de portales ordenados por región (Estructura de la ventana)
//        for (int r = 0; r < regionCount; r++)
//        {
//            regionSizes[r] = graph.GetRegionSize(r);

//            int portalStart = tempPortalsList.Count;
//            List<PortalNode> portalsInRegion = portalGraph.GetPortalsInRegion(r);
//            int portalCountInRegion = 0;

//            foreach (var portal in portalsInRegion)
//            {
//                tempPortalsList.Add(portal.Id); // Guardamos la ID del portal
//                portalCountInRegion++;
//            }

//            regionPortalsOffsets[r] = new int2(portalStart, portalCountInRegion);
//        }

//        var regionPortalsBuffer = new NativeArray<int>(tempPortalsList.Count, Allocator.Persistent);
//        for (int p = 0; p < tempPortalsList.Count; p++) regionPortalsBuffer[p] = tempPortalsList[p];

//        // 5. SOLUCIÓN CRÍTICA: PortalNodes indexado mediante su Portal ID Único
//        // En lugar de usar la longitud del buffer de regiones, usamos el conteo total de portales del grafo clásico
//        int totalPortalsCount = portalGraph.CountPortals(); // Reemplaza esto por tu propiedad clásica para saber cuántos portales hay en total
//        var portalNodes = new NativeArray<int2>(totalPortalsCount, Allocator.Persistent);

//        // Rellenamos basándonos en la ID única que tu objeto clásico tenga asignada
//        for (int id = 0; id < totalPortalsCount; id++)
//        {
//            var portal = portalGraph.GetPortal(id);
//            if (portal != null)
//            {
//                portalNodes[id] = new int2(portal.NodeA, portal.NodeB);
//            }
//        }

//        // 6. Rellenar las aristas del grafo abstracto de portales
//        IEnumerable<(int fromPortalId, int toPortalId, float cost)> edges = portalGraph.GetAllEdges();
//        var portalDistances = new NativeParallelHashMap<int2, float>(edges.Count(), Allocator.Persistent);
//        foreach (var edge in edges)
//        {
//            portalDistances.TryAdd(new int2(edge.fromPortalId, edge.toPortalId), edge.cost);
//        }

//        // 7. Guardar contenedor en el Singleton
//        Entity graphEntity = em.CreateEntity();
//        em.AddComponentData(graphEntity, new NavGraphData
//        {
//            NodeCount = nodeCount,
//            RegionCount = regionCount,
//            NodePositions = positions,
//            NodeCosts = costs,
//            IsWalkableFlags = walkables,
//            NodeRegionIds = regionIds,
//            GlobalToLocalMap = globalToLocal,
//            LocalToGlobalMap = localToGlobal,
//            NeighborsBuffer = neighborsBuffer,
//            NodeNeighborsOffsets = nodeNeighborsOffsets,
//            RegionSizes = regionSizes,
//            RegionPortalsOffsets = regionPortalsOffsets,
//            RegionPortalsBuffer = regionPortalsBuffer,
//            PortalNodes = portalNodes, // Ahora sí mapea perfectamente ID -> Nodos
//            PortalDistances = portalDistances
//        });

//#if UNITY_EDITOR
//        em.SetName(graphEntity, "NavGraphData_Singleton");
//#endif
//    }

//    private void RouteToDOTS(int routeId)
//    {
//        Debug.Log($"[FlowFieldTestController] Calculando ruta para el nodo {routeId} y actualizando GlobalPortalDistances en FlowFieldBridge.");
//        // Aquí se actualiza GlobalPortalDistances en FlowFieldBridge a partir de la ruta calculada en el grafo clásico
//        // Adquirir el grafo clásico y el router jerárquico
//        var graph = provider.Graph;
//        if (graph == null)
//        {
//            Debug.LogError("[FlowFieldTestController] No se pudo obtener el grafo clásico desde el proveedor.");
//            return;
//        }
//        if (!FlowFieldManager.Instance.TryGetContext(graph))
//        {
//            Debug.LogError("[FlowFieldTestController] No se pudo obtener el contexto del grafo clásico.");
//            return;
//        }

//        var context = FlowFieldManager.Instance.GetContext(graph);
//        if (context == null)
//        {
//            Debug.LogError("[FlowFieldTestController] No se pudo obtener el contexto del grafo clásico.");
//            return;
//        }

//        if (!context.FlowFieldCache.ContainsKey(routeId))
//        {
//            // Generemos la ruta si no existe en el cache
//            Debug.Log($"[FlowFieldTestController] La ruta con ID {routeId} no existe en el cache. Generando nueva ruta.");
//            FlowFieldManager.Instance.RegisterRoute(graph, routeId);
//        }

//        var route = context.FlowFieldCache[routeId];
//        var router = context.Router;

//        Debug.Log($"[FlowFieldTestController] Actualizando GlobalPortalDistances para la ruta con ID {routeId}. Portales encontrados: {route.DistanceMaps.Count}.");

//        // Limpiar el GlobalPortalDistances antes de actualizarlo
//        var bridge = FlowFieldBridge.Instance;
//        bridge.GlobalPortalDistances.Clear();

//        // Actualizar GlobalPortalDistances con los portales y sus distancias desde el nodo de destino
//        foreach (var portal in route.DistanceMaps)
//        {
//            bridge.GlobalPortalDistances.Add(portal.Key, portal.Value);
//        }

//        // Fases
//        int targetRegion = navGraph.GetRegionId(routeId);
//        Dictionary<int, int> phases = router.CalculateRegionPhases(targetRegion, route.DistanceMaps);

//        bridge.PhasesMap.Clear();
//        foreach (var item in phases) 
//        {
//            bridge.PhasesMap.Add(item.Key, item.Value);
//        }
//    }
//}