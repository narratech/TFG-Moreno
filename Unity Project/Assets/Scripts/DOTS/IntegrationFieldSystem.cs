using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using BurstCompile = Unity.Burst.BurstCompileAttribute;

namespace DOTSFlowField
{
    /// <summary>
    /// Construye el campo de integración por región usando el algoritmo Eikonal (Fast Marching) de tu motor.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RegionLifecycleSystem))]
    public partial struct IntegrationFieldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NavGraphData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var bridge = FlowFieldBridge.Instance;
            if (bridge == null || !bridge.GlobalPortalDistances.IsCreated)
                return;

            var navGraphData = SystemAPI.GetSingleton<NavGraphData>();

            var integrationJob = new IntegrationFieldsParallelJob
            {
                NavGraph = navGraphData,
                PortalDistances = bridge.GlobalPortalDistances,
                WindowSize = bridge.NumRegionLevelsWindow
            };

            state.Dependency = integrationJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// Copia unmanaged de la estructura NeighborData original de tu motor.
    /// </summary>
    public struct NeighborDataBurst
    {
        public float3 Pos;
        public float T;
        public float Cost;
    }

    [BurstCompile]
    public partial struct IntegrationFieldsParallelJob : IJobEntity
    {
        [ReadOnly] public NavGraphData NavGraph;
        [ReadOnly] public NativeParallelHashMap<int, float> PortalDistances;
        public int WindowSize; // Mantenemos la ventana de tu configuración

        public void Execute(
            ref RegionRouteConfig config,
            ref DynamicBuffer<IntegrationFieldBuffer> integrationBuffer,
            [ReadOnly] in DynamicBuffer<NodeDataBuffer> nodesBuffer)
        {
            // Solo procesamos las regiones marcadas como Requeridas e IsDirty
            if (config.State != RegionState.Required || !config.IsDirty)
                return;

            for (int i = 0; i < integrationBuffer.Length; i++)
                integrationBuffer[i] = float.MaxValue;

            // Usamos una NativePriorityQueue aproximada o en su defecto un Min-Heap para Fast Marching
            // Para mantener compatibilidad directa y óptima con Burst de forma nativa, usamos NativeList como Heap
            var pq = new NativeList<int>(integrationBuffer.Length, Allocator.Temp);

            // Voy a debugear toda las lista PortalDistances para asegurarme que se estén cargando correctamente
            UnityEngine.Debug.Log($"[IntegrationFieldSystem] PortalDistances count: {PortalDistances.Count()} for region {config.RegionId}");
            foreach (var kvp in PortalDistances)
            {
                UnityEngine.Debug.Log($"[IntegrationFieldSystem] PortalDistances key: {kvp.Key}, value: {kvp.Value}");
            }

            // FASE 1: Sembrado de destinos (Portales desde el PortalDistMap global)
            for (int i = 0; i < integrationBuffer.Length; i++)
            {
                int2 localKey = new int2(i, config.RegionId);

                UnityEngine.Debug.Log($"[IntegrationFieldSystem] Checking localKey {localKey} for region {config.RegionId}");

                if (NavGraph.LocalToGlobalMap.TryGetValue(localKey, out int globalNodeId))
                {
                    UnityEngine.Debug.Log($"[IntegrationFieldSystem] Seeding node {globalNodeId} for region {config.RegionId}, num: {PortalDistances.Count()}");
  
                    if (PortalDistances.TryGetValue(globalNodeId, out float portalCost))
                    {
                        integrationBuffer[i] = portalCost;
                        PushHeap(ref pq, globalNodeId, portalCost, ref integrationBuffer);
                    }
                }
            }

            UnityEngine.Debug.Log($"[IntegrationFieldSystem] Region {config.RegionId} seeded with {pq.Length} portal nodes.");

            // FASE 2: Bucle Fast Marching Eikonal
            while (pq.Length > 0)
            {
                int currGlobal = PopHeap(ref pq, ref integrationBuffer);
                int currLocal = NavGraph.GlobalToLocalMap[currGlobal];

                // Extraemos los vecinos de primer nivel
                int2 offsetData = NavGraph.NodeNeighborsOffsets[currGlobal];
                int startIndex = offsetData.x;
                int neighborCount = offsetData.y;

                for (int i = 0; i < neighborCount; i++)
                {
                    int neighborGlobal = NavGraph.NeighborsBuffer[startIndex + i];
                    int nRegion = NavGraph.NodeRegionIds[neighborGlobal];

                    // Validamos que el vecino esté en nuestro rango de cálculo y sea caminable
                    if (nRegion != config.RegionId || !NavGraph.IsWalkableFlags[neighborGlobal])
                        continue;

                    // --- RECOPILACIÓN DE VECINOS ACEPTADOS (Estructuras fijas para Burst) ---
                    // En lugar de List<>, usamos un array nativo de tamaño fijo máximo (ej: 8 vecinos en rejilla)
                    var acceptedNeighbors = new NativeList<NeighborDataBurst>(8, Allocator.Temp);

                    int2 nOffsetData = NavGraph.NodeNeighborsOffsets[neighborGlobal];
                    int nStartIndex = nOffsetData.x;
                    int nNeighborCount = nOffsetData.y;

                    for (int j = 0; j < nNeighborCount; j++)
                    {
                        int nOfN = NavGraph.NeighborsBuffer[nStartIndex + j];
                        int nnRegion = NavGraph.NodeRegionIds[nOfN];

                        if (nnRegion != config.RegionId || !NavGraph.IsWalkableFlags[nOfN])
                            continue;

                        int nnLocal = NavGraph.GlobalToLocalMap[nOfN];
                        float val = integrationBuffer[nnLocal].Value;

                        if (val < float.MaxValue)
                        {
                            acceptedNeighbors.Add(new NeighborDataBurst
                            {
                                Pos = NavGraph.NodePositions[nOfN],
                                T = val,
                                Cost = NavGraph.NodeCosts[nOfN]
                            });
                        }
                    }

                    if (acceptedNeighbors.Length == 0)
                    {
                        acceptedNeighbors.Dispose();
                        continue;
                    }

                    // --- CÁLCULO EIKONAL EXACTO DE TU ENGINE ---
                    float nodeCost = NavGraph.NodeCosts[neighborGlobal];
                    float3 targetPos = NavGraph.NodePositions[neighborGlobal];

                    float newDist = CalculateEikonalCostBurst(targetPos, ref acceptedNeighbors, nodeCost);

                    int nLocal = NavGraph.GlobalToLocalMap[neighborGlobal];
                    if (newDist < integrationBuffer[nLocal].Value)
                    {
                        integrationBuffer[nLocal] = newDist;
                        PushHeap(ref pq, neighborGlobal, newDist, ref integrationBuffer);
                    }

                    acceptedNeighbors.Dispose();
                }
            }

            pq.Dispose();

            // Debugeamos un nodo de prueba para verificar que el cálculo se realizó correctamente

            UnityEngine.Debug.Log($"[IntegrationFieldSystem] Region {config.RegionId} processed. Sample node cost: {integrationBuffer[0].Value}");

            // Pasamos a generado y limpiamos bandera
            config.State = RegionState.Generated;
            config.IsDirty = false;
        }

        // --- TRADUCCIÓN DE TU MATEMÁTICA EIKONAL A BURST ---
        private float CalculateEikonalCostBurst(float3 targetPos, ref NativeList<NeighborDataBurst> neighbors, float localCost)
        {
            // Ordenación por inserción manual (Burst no acepta LINQ OrderBy)
            for (int i = 1; i < neighbors.Length; i++)
            {
                var key = neighbors[i];
                int j = i - 1;
                while (j >= 0 && neighbors[j].T > key.T)
                {
                    neighbors[j + 1] = neighbors[j];
                    j--;
                }
                neighbors[j + 1] = key;
            }

            // Intento 3D (Tetraedro)
            if (neighbors.Length >= 3)
            {
                float t = SolveQuadraticNDBurst(targetPos, ref neighbors, 3, localCost);
                if (!float.IsNaN(t) && t > neighbors[0].T && t > neighbors[1].T && t > neighbors[2].T)
                    return t;
            }

            // Intento 2D (Triángulo)
            if (neighbors.Length >= 2)
            {
                float t = SolveQuadraticNDBurst(targetPos, ref neighbors, 2, localCost);
                if (!float.IsNaN(t) && t > neighbors[0].T && t > neighbors[1].T)
                    return t;
            }

            // 1D Dijkstra Puro
            return neighbors[0].T + (math.distance(targetPos, neighbors[0].Pos) * localCost);
        }

        private float SolveQuadraticNDBurst(float3 pC, ref NativeList<NeighborDataBurst> pts, int count, float f)
        {
            float a = 0, b = 0, c = -f * f;

            for (int i = 0; i < count; i++)
            {
                float3 v = pts[i].Pos - pC;
                float d = math.length(v);
                if (d < 0.001f) d = 0.001f;

                float dSq = d * d;
                a += 1f / dSq;
                b -= 2f * pts[i].T / dSq;
                c += (pts[i].T * pts[i].T) / dSq;
            }

            float disc = (b * b) - (4f * a * c);
            if (disc < 0) return float.NaN;

            return (-b + math.sqrt(disc)) / (2f * a);
        }

        // --- FUNCIONES AUXILIARES MIN-HEAP NATIVAS (Para evitar colas administradas en Burst) ---
        private void PushHeap(ref NativeList<int> heap, int globalNode, float cost, ref DynamicBuffer<IntegrationFieldBuffer> buffer)
        {
            heap.Add(globalNode);
            int i = heap.Length - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                int localI = NavGraph.GlobalToLocalMap[heap[i]];
                int localP = NavGraph.GlobalToLocalMap[heap[p]];
                if (buffer[localI].Value >= buffer[localP].Value) break;

                int temp = heap[i]; heap[i] = heap[p]; heap[p] = temp;
                i = p;
            }

        }

        private int PopHeap(ref NativeList<int> heap, ref DynamicBuffer<IntegrationFieldBuffer> buffer)
        {
            int top = heap[0];
            heap[0] = heap[heap.Length - 1];
            heap.RemoveAt(heap.Length - 1);

            int i = 0;
            while (i * 2 + 1 < heap.Length)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int target = left;

                if (right < heap.Length)
                {
                    int localL = NavGraph.GlobalToLocalMap[heap[left]];
                    int localR = NavGraph.GlobalToLocalMap[heap[right]];
                    if (buffer[localR].Value < buffer[localL].Value) target = right;
                }

                int localI = NavGraph.GlobalToLocalMap[heap[i]];
                int localT = NavGraph.GlobalToLocalMap[heap[target]];
                if (buffer[localI].Value <= buffer[localT].Value) break;

                int temp = heap[i]; heap[i] = heap[target]; heap[target] = temp;
                i = target;
            }
            return top;
        }
    }
}