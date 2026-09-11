using UnityEngine;

/// <summary>
/// Agente de navegación con Arrival de rotación y movimiento libre de oscilaciones.
/// </summary>
public class NavAgent : MonoBehaviour
{
    [Header("Provider")]
    [SerializeField] private NavGraphProvider _provider;

    [Header("Movement Settings")]
    [SerializeField] public float MaxSpeed = 5f;
    [SerializeField] public float MaxForce = 20f;

    [Header("Angular Dynamics")]
    [Tooltip("Velocidad angular máxima en grados/segundo")]
    [SerializeField] public float MaxAngularSpeed = 360f;

    [Tooltip("Ángulo a partir del cual el giro comienza a reducirse de 1 a 0 (Arrival)")]
    [SerializeField] public float RotationArrivalThreshold = 45f;

    [Header("Edge Constraint Settings")]
    [Tooltip("Distancia mínima de seguridad respecto a los nodos no caminables.")]
    [SerializeField] public float BoundaryPadding = 0.15f;

    private readonly int[] _interpolationNodes = new int[8];
    private IAgentSteering[] _steerings;

    public INavGraph Graph { get; private set; }
    public int TargetNode { get; private set; } = -1;
    public int CurrentNode { get; private set; }
    public int CurrentRegion { get; private set; }
    public Vector3 Velocity { get; private set; } = Vector3.zero;
    public Vector3 SteeringForce { get; private set; } = Vector3.zero;

    private void Awake()
    {
        if (Graph == null && _provider != null) AssignGraph(_provider.Graph);
        _steerings = GetComponents<IAgentSteering>();
    }

    private void Start()
    {
        if (Graph == null && _provider != null) AssignGraph(_provider.Graph);
        if (TargetNode >= 0) AgentManager.Instance?.Subscribe(this);
    }

    public void AssignGraph(INavGraph graph) => Graph = graph;

    private void OnDestroy()
    {
        if (AgentManager.Instance != null) AgentManager.Instance.Unsubscribe(this);
    }

    private void Update()
    {
        if (Graph == null) return;

        CurrentNode = Graph.GetClosestNode(transform.position);
        CurrentRegion = Graph.GetRegionId(CurrentNode);

        SteeringForce = ComputeSteering();

        // 1. LA MAGIA: Mirar a la velocidad anticipada, NO a la fuerza de frenado.
        // Esto evita el temblor de 180º al detenerse.
        Vector3 desiredDirection = Velocity + SteeringForce;

        Vector3 surfaceNormal = Graph.GetNodeNormal(CurrentNode);
        bool isVolumetric = surfaceNormal.sqrMagnitude < 0.0001f;

        if (!isVolumetric)
        {
            surfaceNormal.Normalize();
            desiredDirection = Vector3.ProjectOnPlane(desiredDirection, surfaceNormal);
        }

        if (EvaluateUnwalkableNodesNormal(transform.position, isVolumetric ? Vector3.zero : surfaceNormal, out Vector3 wallNormal, out float penetrationDepth))
        {
            if (Vector3.Dot(desiredDirection, wallNormal) < 0f)
            {
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, wallNormal);
            }
        }

        // 2. LÓGICA DE ROTACIÓN (Tu Arrival)
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            Quaternion targetRotation;
            if (isVolumetric)
            {
                targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
            }
            else
            {
                Vector3 projectedDir = Vector3.ProjectOnPlane(desiredDirection, surfaceNormal).normalized;
                if (projectedDir.sqrMagnitude < 0.0001f) projectedDir = transform.forward;
                targetRotation = Quaternion.LookRotation(projectedDir, surfaceNormal);
            }

            float angleDiff = Quaternion.Angle(transform.rotation, targetRotation);
            float maxStepThisFrame = MaxAngularSpeed * Time.deltaTime;
            float rotationStep;

            // TU ARRIVAL: Factor de 1 a 0
            if (angleDiff < RotationArrivalThreshold && RotationArrivalThreshold > 0f)
            {
                float factor = angleDiff / RotationArrivalThreshold;
                rotationStep = maxStepThisFrame * factor;
            }
            else
            {
                rotationStep = maxStepThisFrame; // Velocidad máxima si está lejos
            }

            // Aplicar y forzar finalización sin temblores (jitter)
            if (angleDiff <= rotationStep || angleDiff < 0.1f)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationStep);
            }

            // 3. AVANCE Y VELOCIDAD
            // Penalizamos la velocidad si el agente aún no está mirando a donde tiene que ir
            float forwardMatch = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDirection));
            Vector3 targetVelocity = transform.forward * (MaxSpeed * forwardMatch);
            Velocity = Vector3.MoveTowards(Velocity, targetVelocity, MaxForce * Time.deltaTime);
        }
        else
        {
            // Si desiredDirection es 0 (ej. está frenando en el objetivo final)
            Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, MaxForce * Time.deltaTime);
        }

        // 4. PREDICCIÓN Y RESTRICCIONES (Look-ahead collision)

        // Empuje clásico por si otro agente lo ha empujado dentro del muro
        if (wallNormal.sqrMagnitude > 0.0001f)
        {
            float velDot = Vector3.Dot(Velocity, wallNormal);
            if (velDot < 0f) Velocity -= wallNormal * velDot;
            if (penetrationDepth > 0f) transform.position += wallNormal * penetrationDepth;
        }

        if (Velocity.sqrMagnitude > 0.0001f)
        {
            // PROYECCIÓN: ¿Dónde estaremos el próximo frame?
            Vector3 nextPos = transform.position + Velocity * Time.deltaTime;
            int nextNode = Graph.GetClosestNode(nextPos);

            // Si el siguiente frame nos mete en un nodo prohibido...
            if (nextNode >= 0 && !Graph.IsWalkable(nextNode))
            {
                // Calculamos una normal de emergencia desde el centro de ese muro hacia nosotros
                Vector3 wallPos = Graph.GetNodePosition(nextNode);
                Vector3 dirFromWall = (transform.position - wallPos);

                Vector3 emergencyWallNorm = isVolumetric
                    ? dirFromWall.normalized
                    : Vector3.ProjectOnPlane(dirFromWall, surfaceNormal).normalized;

                // Anulamos toda la velocidad que nos empuja hacia el choque fatal (Sliding preventivo)
                float velDot = Vector3.Dot(Velocity, emergencyWallNorm);
                if (velDot < 0f) Velocity -= emergencyWallNorm * velDot;

                // Recalculamos la posición segura final
                nextPos = transform.position + Velocity * Time.deltaTime;
            }

            transform.position = nextPos;
        }

        // 5. Restricciones finales dictadas por el grafo (terreno, gravedad, etc.)
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = Velocity;

        Graph.ConstrainPositionAndRotation(ref position, ref velocity, ref rotation);

        transform.SetPositionAndRotation(position, rotation);
        Velocity = velocity;
    }

    private bool EvaluateUnwalkableNodesNormal(Vector3 position, Vector3 surfaceNormal, out Vector3 wallNormal, out float penetrationDepth)
    {
        wallNormal = Vector3.zero;
        penetrationDepth = 0f;

        int nodeCount = Graph.GetInterpolationNodes(position, _interpolationNodes);
        if (nodeCount <= 0) return false;

        Vector3 accumNormal = Vector3.zero;
        float totalWeight = 0f;
        float maxPenetration = 0f;

        bool isVolumetric = surfaceNormal.sqrMagnitude < 0.0001f;

        for (int i = 0; i < nodeCount; i++)
        {
            int node = _interpolationNodes[i];

            if (Graph.IsWalkable(node)) continue;

            Vector3 nodePos = Graph.GetNodePosition(node);
            Vector3 diff = position - nodePos;

            Vector3 collisionDiff = isVolumetric ? diff : Vector3.ProjectOnPlane(diff, surfaceNormal);
            float dist = collisionDiff.magnitude;

            if (dist < 0.0001f)
            {
                collisionDiff = isVolumetric ? -transform.forward : Vector3.ProjectOnPlane(-transform.forward, surfaceNormal);
                if (collisionDiff.sqrMagnitude < 0.0001f)
                {
                    collisionDiff = isVolumetric ? Random.onUnitSphere : Vector3.ProjectOnPlane(Random.onUnitSphere, surfaceNormal);
                }
                dist = 0.01f;
            }

            Vector3 dirFromObstacle = collisionDiff / dist;
            float weight = 1f / (dist * dist);

            accumNormal += dirFromObstacle * weight;
            totalWeight += weight;

            float overlap = BoundaryPadding - dist;
            if (overlap > maxPenetration)
            {
                maxPenetration = overlap;
            }
        }

        if (totalWeight > 0f && accumNormal.sqrMagnitude > 0.0001f)
        {
            wallNormal = accumNormal.normalized;
            penetrationDepth = Mathf.Max(0f, maxPenetration);
            return true;
        }

        return false;
    }

    private Vector3 ComputeSteering()
    {
        Vector3 force = Vector3.zero;
        foreach (IAgentSteering steering in _steerings)
        {
            if (steering == null || !steering.enabled) continue;
            force += steering.GetForce() * steering.Weight;
        }
        return Vector3.ClampMagnitude(force, MaxForce);
    }

    public void SetDestination(int targetNode)
    {
        if (TargetNode == targetNode) return;
        TargetNode = targetNode;
        AgentManager.Instance?.Subscribe(this);
    }

    public void SetProvider(NavGraphProvider provider)
    {
        _provider = provider;
        if (provider != null) AssignGraph(_provider.Graph);
    }
}