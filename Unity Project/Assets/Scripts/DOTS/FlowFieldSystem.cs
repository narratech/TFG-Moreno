using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using BurstCompile = Unity.Burst.BurstCompileAttribute;
using UnityEngine;

namespace DOTSFlowField
{
    /// <summary>
    /// Sistema principal que aplica el flow field a los agentes usando regiones activas.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(IntegrationFieldSystem))]
    public partial struct FlowFieldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavGraphData>(); // <--- Asegura que el sistema solo se ejecute si existe un NavGraphData
        }

        public void OnUpdate(ref SystemState state)
        {
            var bridge = FlowFieldBridge.Instance;
            if (bridge == null || bridge.gridNavGraph == null) return;

            var regionQuery = SystemAPI.QueryBuilder()
                .WithAll<RegionRouteConfig, FlowDirectionBuffer>()
                .Build();

            // Captura segura del Singleton gracias al RequireForUpdate
            var navGraphData = SystemAPI.GetSingleton<NavGraphData>();

            var regionEntities = regionQuery.ToEntityArray(Allocator.TempJob);
            var regionConfigs = regionQuery.ToComponentDataArray<RegionRouteConfig>(Allocator.TempJob);

            var activeRegionsLookup = new NativeParallelHashMap<int2, Entity>(
                regionEntities.Length,
                Allocator.TempJob);

            for (int i = 0; i < regionEntities.Length; i++)
            {
                var config = regionConfigs[i];

                if (config.RouteEntity == Entity.Null)
                    continue;

                if (config.State == RegionState.Generated ||
                    config.State == RegionState.Required)
                {
                    int2 key = new int2(config.RouteEntity.Index, config.RegionId);
                    activeRegionsLookup.TryAdd(key, regionEntities[i]);
                }
            }

            var moveJob = new MoveAgentsJob
            {
                NavGraph = navGraphData,
                ActiveRegionsLookup = activeRegionsLookup,
                IntegrationFieldsLookup = SystemAPI.GetBufferLookup<IntegrationFieldBuffer>(true),
                CurrentRouteEntity = bridge.CurrentActiveRouteEntity,
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            state.Dependency = moveJob.ScheduleParallel(state.Dependency);

            regionEntities.Dispose(state.Dependency);
            regionConfigs.Dispose(state.Dependency);
            activeRegionsLookup.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Mueve los agentes siguiendo el integration field de la región activa mediante Gradient Descent.
    /// </summary>
    [BurstCompile]
    public partial struct MoveAgentsJob : IJobEntity
    {
        [ReadOnly] public NavGraphData NavGraph;
        [ReadOnly] public NativeParallelHashMap<int2, Entity> ActiveRegionsLookup;
        [ReadOnly] public BufferLookup<IntegrationFieldBuffer> IntegrationFieldsLookup; // Contiene FLOATS, no vectores
        public Entity CurrentRouteEntity;
        public float DeltaTime;

        public void Execute(ref LocalTransform transform, in AgentMovementData agent)
        {
            // 1. Validaciones iniciales (Corregido a ActiveRouteId según tu Baker)
            if (agent.RouteId == -1 || CurrentRouteEntity == Entity.Null)
                return;

            // 2. Conseguimos el nodo global bajo los pies del agente
            int globalNodeIndex = NavGraph.GetClosestNode(transform.Position);
            if (globalNodeIndex == -1) return;

            int currentRegionId = NavGraph.NodeRegionIds[globalNodeIndex];

            // 3. Buscamos el contenedor de la región para esta ruta
            int2 key = new int2(CurrentRouteEntity.Index, currentRegionId);
            if (!ActiveRegionsLookup.TryGetValue(key, out Entity regionContainer)) return;
            if (!IntegrationFieldsLookup.HasBuffer(regionContainer)) return;

            var integrationBuffer = IntegrationFieldsLookup[regionContainer];
            int localNodeIndex = NavGraph.GlobalToLocalMap[globalNodeIndex];

            if (localNodeIndex < 0 || localNodeIndex >= integrationBuffer.Length)
                return;

            // Coste numérico actual de la celda donde está el agente
            float currentCost = integrationBuffer[localNodeIndex].Value;

            // -------------------------------------------------------------------------
            // BÚSQUEDA DEL VECINO MÁS BARATO (Gradient Descent / Caída de pendiente)
            // -------------------------------------------------------------------------
            float3 bestDirection = float3.zero;
            float bestCost = currentCost;

            // Extraemos los offsets de los vecinos de este nodo desde nuestro NavGraph plano
            int2 offsetData = NavGraph.NodeNeighborsOffsets[globalNodeIndex];
            int startIndex = offsetData.x;
            int neighborCount = offsetData.y;

            for (int i = 0; i < neighborCount; i++)
            {
                int neighborGlobalIndex = NavGraph.NeighborsBuffer[startIndex + i];

                // Evaluamos solo vecinos que pertenezcan a la misma región
                if (NavGraph.NodeRegionIds[neighborGlobalIndex] == currentRegionId)
                {
                    if (!NavGraph.IsWalkableFlags[neighborGlobalIndex]) continue;

                    int neighborLocalIndex = NavGraph.GlobalToLocalMap[neighborGlobalIndex];
                    float neighborCost = integrationBuffer[neighborLocalIndex].Value;

                    // Si el vecino tiene un coste menor que nuestro récord actual, queremos ir hacia él
                    if (neighborCost < bestCost)
                    {
                        bestCost = neighborCost;
                        // Vector que apunta hacia la posición física del vecino ideal
                        bestDirection = NavGraph.NodePositions[neighborGlobalIndex] - transform.Position;
                    }
                }
            }
            //UnityEngine.Debug.Log($"Agent at {transform.Position} moving towards {currentCost} in region {currentRegionId}");

            // 4. Aplicar movimiento si encontramos una dirección cuesta abajo válida
            if (math.lengthsq(bestDirection) <= 0.001f)
                return;

            bestDirection.y = 0; // Evitamos que vuelen o se hundan en el plano Y
            float3 velocity = math.normalize(bestDirection) * agent.Speed;
            transform.Position += velocity * DeltaTime;
        }
    }
}