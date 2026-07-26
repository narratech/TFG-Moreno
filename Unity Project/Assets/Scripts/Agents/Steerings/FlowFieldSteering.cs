using System;
using UnityEngine;

public class FlowFieldSteering : IAgentSteering
{
    [Header("Settings")]
    [SerializeField]
    private FlowFieldSteeringSettings settings;

    [Header("Manual Settings")]
    [SerializeField]
    private float _stepSize = 0.5f;

    [SerializeField]
    private Vector3 _desiredOffset;

    private readonly int[] _nodes = new int[8];

    private Vector3 _samplePosition;
    private Vector3 _lastAgentPosition;

    private int _currentSteps = 0;

    private float StepSize =>
            settings != null
            ? settings.StepSize
            : _stepSize;

    public void Start()
    {
        _lastAgentPosition = transform.position;
        _currentSteps = 0;
    }

    public override Vector3 GetDirection(FlowFieldAgent agent)
    {
        if (agent.TargetNode < 0)
            return Vector3.zero;

        // Comprobar si el agente ha avanzado la distancia StepSize para recalcular
        if ((agent.transform.position - _lastAgentPosition).sqrMagnitude >= StepSize * StepSize)
        {
            _lastAgentPosition = agent.transform.position;
            UpdateSamplePosition(agent);
        }

        Vector3 flowDirection = SampleFlowField(agent, _samplePosition);

        if (flowDirection.sqrMagnitude < 0.05f)
            return -agent.Velocity;

        flowDirection.Normalize();

        // Evitar que la dirección apunte frontalmente a una pared
        INavGraph graph = agent.Graph;
        Vector3 probePos = agent.transform.position + flowDirection * StepSize;
        int probeNode = graph.GetClosestNode(probePos);

        if (!graph.IsWalkable(probeNode))
        {
            Vector3 wallPos = graph.GetNodePosition(probeNode);
            Vector3 agentSafePos = graph.GetNodePosition(agent.CurrentNode);
            Vector3 wallNormal = (agentSafePos - wallPos).normalized;

            if (wallNormal != Vector3.zero)
            {
                // Deslizar paralelamente a la pared
                flowDirection = Vector3.ProjectOnPlane(flowDirection, wallNormal).normalized;
            }
            else
            {
                flowDirection = Vector3.zero;
            }
        }

        if (flowDirection == Vector3.zero)
            return -agent.Velocity;

        Vector3 desiredVelocity = flowDirection * agent.MaxSpeed;

        return desiredVelocity - agent.Velocity;
    }

    private void UpdateSamplePosition(FlowFieldAgent agent)
    {
        INavGraph graph = agent.Graph;

        // Posición base segura del agente (Paso 0)
        Vector3 position = agent.transform.position;
        if (!graph.IsWalkable(agent.CurrentNode))
        {
            position = graph.GetNodePosition(agent.CurrentNode);
        }

        Vector3 offset = _desiredOffset;
        Vector3 normal = graph.GetNodeNormal(agent.CurrentNode);

        if (normal != Vector3.zero)
        {
            float dot = Vector3.Dot(normal, Vector3.up);
            if (dot < 0.9999f)
                offset = Quaternion.FromToRotation(Vector3.up, normal) * offset;
        }

        float distance = offset.magnitude;

        // Si no hay offset configurado, usamos el paso 0 directamente
        if (distance < 0.001f)
        {
            _currentSteps = 0;
            _samplePosition = position;
            return;
        }

        Vector3 offsetDir = offset.normalized;
        int desiredSteps = Mathf.CeilToInt(distance / StepSize);

        // Buscamos el mayor paso válido desde 1 hasta desiredSteps
        int bestValidStep = 0; // Por defecto es 0 (posición del agente)

        for (int step = 1; step <= desiredSteps; step++)
        {
            Vector3 sample = position + offsetDir * (step * StepSize);
            int sampleNode = graph.GetClosestNode(sample);

            // El propio punto del offset cae en una pared? -> Inválido.
            if (!graph.IsWalkable(sampleNode))
                break;

            // Simulamos los N pasos de flujo que saldrían de este 'sample'
            Vector3 simulatedPosition = sample;
            bool predictionHitWall = false;
            const int predictionSteps = 6;

            for (int i = 0; i < predictionSteps; i++)
            {
                Vector3 flow = SampleFlowField(agent, simulatedPosition);

                if (flow.sqrMagnitude < 0.0001f)
                    break;

                flow.Normalize();
                simulatedPosition += flow * StepSize;

                int simulatedNode = graph.GetClosestNode(simulatedPosition);

                // Si la trayectoria trazada desde el sample CHOCA -> Inválido
                if (!graph.IsWalkable(simulatedNode))
                {
                    predictionHitWall = true;
                    break;
                }
            }

            if (predictionHitWall)
                break;

            // Si pasó todas las pruebas, este paso es seguro
            bestValidStep = step;
        }

        // Si la pared se acerca, colapsamos o nos ajustamos INMEDIATAMENTE al paso válido más lejano.
        _currentSteps = bestValidStep;

        _samplePosition = position + offsetDir * (_currentSteps * StepSize);
    }

    private Vector3 SampleFlowField(FlowFieldAgent agent, Vector3 samplePosition)
    {
        INavGraph graph = agent.Graph;

        int count = graph.GetInterpolationNodes(samplePosition, _nodes);
        Vector3 direction = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < count; i++)
        {
            int node = _nodes[i];
            Vector3 nodePosition = graph.GetNodePosition(node);
            float sqrDistance = (samplePosition - nodePosition).sqrMagnitude;

            // Proteger de divisiones cerca de cero
            float weight = 1f / (sqrDistance + 0.0001f);

            if (!graph.IsWalkable(node))
            {
                Vector3 delta = samplePosition - nodePosition;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    direction += delta.normalized * weight;
                    totalWeight += weight;
                }
                continue;
            }

            int region = graph.GetRegionId(node);
            FlowField field = FlowFieldManager.Instance.GetFlowField(graph, region, agent.TargetNode);

            if (field == null)
            {
                field = FlowFieldEngine.GenerateFlowPath(graph, agent.TargetNode, node);
            }

            if (field == null)
                continue;

            direction += field.FlowDirections[graph.GetLocalNode(node)] * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0f)
            direction /= totalWeight;

        return direction;
    }

    public void SetDesiredOffset(Vector3 vec)
    {
        _desiredOffset = vec;
    }

    private void OnDrawGizmosSelected()
    {
        // Solo dibuja si el juego está en ejecución
        if (!Application.isPlaying) return;

        // 1. Línea desde el agente hasta el punto de muestra
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, _samplePosition);

        // 2. Esfera en el punto donde se lee el FlowField
        Gizmos.color = _currentSteps > 0 ? Color.green : Color.red;
        Gizmos.DrawWireSphere(_samplePosition, StepSize * 0.5f);

        // 3. Dirección resultante del flujo muestreado
        FlowFieldAgent agent = GetComponent<FlowFieldAgent>();
        if (agent != null && agent.Graph != null)
        {
            Vector3 flow = SampleFlowField(agent, _samplePosition);
            if (flow.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(_samplePosition, flow.normalized * 1.5f);

                // Dirección que realmente se le entrega al agente tras pasar el filtro
                Vector3 dir = GetDirection(agent);
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(transform.position, dir.normalized * 2f);
            }
        }
    }
}