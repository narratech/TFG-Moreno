using UnityEngine;

public class FlowFieldSteering : IAgentSteering
{
    [Header("Settings")]
    [SerializeField]
    private FlowFieldSteeringSettings settings;

    [Header("Manual Settings")]
    [SerializeField]
    private float _stepSize = 1f;

    [SerializeField]
    private float _timeStamp = 0.1f;
    private float _time = 0f;

    [SerializeField]
    private Vector3 _desiredOffset;

    private readonly int[] _nodes = new int[8];

    private Vector3 _samplePosition;
    private Vector3 _lastAgentPosition;

    private int _currentSteps = 0;

    private float StepSize => settings != null ? settings.StepSize : _stepSize;
    private float TimeStamp => settings != null ? settings.TimeStamp : _timeStamp;

    public void Start()
    {
        _lastAgentPosition = transform.position;
        _currentSteps = 0;
        _time = Random.Range(0f, TimeStamp);
    }

    public override Vector3 GetDirection(NavAgent agent)
    {
        if (agent.TargetNode < 0)
            return Vector3.zero;

        // Guardamos referencias locales para evitar llamadas a propiedades de Unity
        Transform agentTransform = agent.transform;
        Vector3 agentPos = agentTransform.position;
        INavGraph graph = agent.Graph;

        _time += Time.deltaTime;
        if (_time > TimeStamp)
        {
            UpdateSamplePosition(agent);
            _lastAgentPosition = agentPos;
            _time = 0f;
        }

        Vector3 flowDirection = SampleFlowField(agent, _samplePosition);

        if (flowDirection.sqrMagnitude < 0.05f)
            return -agent.Velocity;

        flowDirection.Normalize();

        // Evitar que la dirección apunte frontalmente a una pared
        float stepSize = StepSize;
        Vector3 probePos = agentPos + flowDirection * stepSize;
        int probeNode = graph.GetClosestNode(probePos);

        if (!graph.IsWalkable(probeNode))
        {
            Vector3 obstaclePos = graph.GetNodePosition(probeNode);
            Vector3 agentSafePos = graph.GetNodePosition(agent.CurrentNode);
            Vector3 surfaceNormal = graph.GetNodeNormal(agent.CurrentNode);

            Vector3 toAgent = agentSafePos - obstaclePos;
            if (toAgent.sqrMagnitude < 0.0001f)
                toAgent = -flowDirection;

            // Proyección agnóstica a la superficie
            Vector3 wallNormal = Vector3.ProjectOnPlane(toAgent, surfaceNormal).normalized;

            if (wallNormal.sqrMagnitude > 0.0001f)
            {
                // Deslizar paralelamente a la pared
                flowDirection = Vector3.ProjectOnPlane(flowDirection, wallNormal).normalized;
            }
            else
            {
                flowDirection = Vector3.zero;
            }
        }

        if (flowDirection.sqrMagnitude < 0.0001f)
            return -agent.Velocity;

        Vector3 desiredVelocity = flowDirection * agent.MaxSpeed;
        return desiredVelocity - agent.Velocity;
    }

    private void UpdateSamplePosition(NavAgent agent)
    {
        INavGraph graph = agent.Graph;
        Vector3 position = agent.transform.position;

        // Early Exit: Si el agente apenas se ha movido, no recalculamos
        if ((position - _lastAgentPosition).sqrMagnitude < 0.001f && _currentSteps > 0)
        {
            return;
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

        if (distance < 0.001f)
        {
            _currentSteps = 0;
            _samplePosition = position;
            return;
        }

        float stepSize = StepSize;
        float invStepSize = 1f / stepSize;

        Vector3 offsetDir = offset / distance;
        int maxSteps = Mathf.CeilToInt(distance * invStepSize);

        float speed = agent.Velocity.magnitude;
        int maxPredictionSteps = Mathf.Clamp(Mathf.RoundToInt(speed * 3f * invStepSize), 1, 6);

        int bestValidStep = 0;

        // 💡 BUCLE INVERSO: Empezamos buscando desde el punto MÁS LEJANO (maxSteps) hacia el agente (1)
        for (int step = maxSteps; step >= 1; step--)
        {
            Vector3 sample = position + offsetDir * (step * stepSize);
            int sampleNode = graph.GetClosestNode(sample);

            // 1. Si la propia posición lejana cae en muro, descartamos esta distancia y probamos una más cercana
            if (!graph.IsWalkable(sampleNode))
                continue;

            // 2. Leemos flujo del sample
            Vector3 flowFromSample = SampleFlowField(agent, sample);
            if (flowFromSample.sqrMagnitude < 0.0001f)
                continue;

            Vector3 flowDir = flowFromSample.normalized;
            Vector3 simulatedPosition = position;
            bool predictionHitWall = false;

            // 3. Simulamos avance del agente en esa dirección
            for (int i = 1; i <= maxPredictionSteps; i++)
            {
                simulatedPosition += flowDir * stepSize;
                int simulatedNode = graph.GetClosestNode(simulatedPosition);

                if (!graph.IsWalkable(simulatedNode))
                {
                    predictionHitWall = true;
                    break;
                }
            }

            // Si la trayectoria no choca, ¡ENCONTRAMOS EL MEJOR PASO!
            // Como íbamos de más lejano a más cercano, el primero que funcione es matemáticamente el MÁXIMO posible.
            if (!predictionHitWall)
            {
                bestValidStep = step;
                break; // 🚀 Cortamos el bucle inmediatamente
            }
        }

        _currentSteps = bestValidStep;
        _samplePosition = position + offsetDir * (_currentSteps * stepSize);
    }

    private Vector3 SampleFlowField(NavAgent agent, Vector3 samplePosition)
    {
        INavGraph graph = agent.Graph;
        int count = graph.GetInterpolationNodes(samplePosition, _nodes);

        Vector3 direction = Vector3.zero;
        float totalWeight = 0f;

        int targetNode = agent.TargetNode;
        FlowFieldManager fieldManager = FlowFieldManager.Instance;

        for (int i = 0; i < count; i++)
        {
            int node = _nodes[i];
            Vector3 nodePosition = graph.GetNodePosition(node);

            // Calculo directo de distancia al cuadrado
            float dx = samplePosition.x - nodePosition.x;
            float dy = samplePosition.y - nodePosition.y;
            float dz = samplePosition.z - nodePosition.z;
            float sqrDistance = dx * dx + dy * dy + dz * dz;

            float weight = 1f / (sqrDistance + 0.0001f);

            if (!graph.IsWalkable(node))
            {
                if (sqrDistance > 0.0001f)
                {
                    float invDist = 1f / Mathf.Sqrt(sqrDistance);
                    Vector3 deltaNorm = new Vector3(dx * invDist, dy * invDist, dz * invDist);
                    direction += deltaNorm * weight;
                    totalWeight += weight;
                }
                continue;
            }

            int region = graph.GetRegionId(node);
            FlowField field = fieldManager.GetFlowField(graph, region, targetNode);

            if (field == null)
            {
                field = FlowFieldEngine.GenerateFlowPath(graph, targetNode, node);
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
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, _samplePosition);

        Gizmos.color = _currentSteps > 0 ? Color.green : Color.red;
        Gizmos.DrawWireSphere(_samplePosition, StepSize * 0.5f);

        NavAgent agent = GetComponent<NavAgent>();
        if (agent != null && agent.Graph != null)
        {
            Vector3 flow = SampleFlowField(agent, _samplePosition);
            if (flow.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(_samplePosition, flow.normalized * 1.5f);
            }
        }
    }
}