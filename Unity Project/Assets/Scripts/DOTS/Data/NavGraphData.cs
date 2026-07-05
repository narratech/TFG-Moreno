using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTSFlowField
{
    /// <summary>
    /// Copia unmanaged y optimizada para Burst de cualquier INavGraph clásico.
    /// </summary>
    public struct NavGraphData : IComponentData
    {
        public int NodeCount;
        public int RegionCount;

        // --- Datos de Nodos (Indexados por GlobalNodeIndex) ---
        [ReadOnly] public NativeArray<float3> NodePositions;
        [ReadOnly] public NativeArray<float> NodeCosts;
        [ReadOnly] public NativeArray<bool> IsWalkableFlags;
        [ReadOnly] public NativeArray<int> NodeRegionIds; // Mapeo: GlobalNode -> RegionId

        // --- Transformaciones Locales/Globales ---
        // Clave: int2(LocalNodeIndex, RegionId) -> Valor: GlobalNodeIndex
        [ReadOnly] public NativeParallelHashMap<int2, int> LocalToGlobalMap;
        // Mapeo: GlobalNode -> LocalNodeIndex dentro de su región
        [ReadOnly] public NativeArray<int> GlobalToLocalMap;

        // --- Estructura de Vecinos (Grafo de adyacencia aplanado para Burst) ---
        // Como cada nodo tiene un número variable de vecinos, usamos un formato de array plano + offsets
        [ReadOnly] public NativeArray<int> NeighborsBuffer; // Todos los vecinos juntos
        [ReadOnly] public NativeArray<int2> NodeNeighborsOffsets; // X: Inicio en NeighborsBuffer, Y: Cantidad

        // --- Datos de Región ---
        [ReadOnly] public NativeArray<int> RegionSizes; // Cantidad de nodos por región

        [ReadOnly] public NativeArray<int2> RegionPortalsOffsets;

        // Array plano que guarda los IDs de los nodos globales que son portales
        [ReadOnly] public NativeArray<int> RegionPortalsBuffer;

        /// <summary>
        /// Método rápido compatible con Burst para obtener el nodo global más cercano.
        /// (Búsqueda por fuerza bruta optimizada en SIMD, o aproximada si tu espacio lo permite)
        /// </summary>
        public int GetClosestNode(float3 worldPosition)
        {
            int closestIndex = -1;
            float minDistance = float.MaxValue;

            for (int i = 0; i < NodePositions.Length; i++)
            {
                if (!IsWalkableFlags[i]) continue;

                float distSq = math.distancesq(worldPosition, NodePositions[i]);
                if (distSq < minDistance)
                {
                    minDistance = distSq;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }

        // Liberación manual de la memoria nativa cuando el grafo cambie o se destruya
        public void Dispose()
        {
            if (NodePositions.IsCreated) NodePositions.Dispose();
            if (NodeCosts.IsCreated) NodeCosts.Dispose();
            if (IsWalkableFlags.IsCreated) IsWalkableFlags.Dispose();
            if (NodeRegionIds.IsCreated) NodeRegionIds.Dispose();
            if (LocalToGlobalMap.IsCreated) LocalToGlobalMap.Dispose();
            if (GlobalToLocalMap.IsCreated) GlobalToLocalMap.Dispose();
            if (NeighborsBuffer.IsCreated) NeighborsBuffer.Dispose();
            if (NodeNeighborsOffsets.IsCreated) NodeNeighborsOffsets.Dispose();
            if (RegionSizes.IsCreated) RegionSizes.Dispose();
        }
    }
}