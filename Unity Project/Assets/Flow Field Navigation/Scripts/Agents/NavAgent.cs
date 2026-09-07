using UnityEngine;

/// <summary>
/// Agente de navegación que gestiona el movimiento, dinámica angular y restricciones sobre el grafo.
/// </summary>
public class NavAgent : MonoBehaviour
{
    [Header("Provider")]
    [SerializeField] private NavGraphProvider _provider;

    [Header("Movement Settings")]
    [SerializeField] public float MaxSpeed = 5f;
    [SerializeField] public float MaxForce = 20f;

    [Header("Angular Dynamics (Giro Físico)")]
    [Tooltip("Velocidad angular máxima en grados/segundo")]
    [SerializeField] public float MaxAngularSpeed = 240f;

    [Tooltip("Frecuencia de respuesta del giro. Mayor número = gira más rápido.")]
    [SerializeField] public float AngularFrequency = 12f;

    [Tooltip("Amortiguación del giro. 1 = amortiguación crítica.")]
    [SerializeField] public float AngularDamping = 1.0f;

    [Tooltip("Sensibilidad de frenado en giros. Mayor número = reduce más la velocidad.")]
    [Range(1f, 4f)]
    [SerializeField] public float TurnTightness = 2f;

    [Header("Edge Constraint Settings")]
    [Tooltip("Distancia mínima de seguridad respecto a los nodos no caminables.")]
    [SerializeField] public float BoundaryPadding = 0.15f;

    private readonly int[] _interpolationNodes = new int[8];

    private IAgentSteering[] _steerings;
    private float _currentAngularSpeed = 0f;

    /// <summary> Grafo de navegación asignado al agente. </summary>
    public INavGraph Graph { get; private set; }

    /// <summary> Nodo de destino actual. </summary>
    public int TargetNode { get; private set; } = -1;

    /// <summary> Nodo en el que se encuentra el agente. </summary>
    public int CurrentNode { get; private set; }

    /// <summary> Región actual del grafo. </summary>
    public int CurrentRegion { get; private set; }

    /// <summary> Velocidad lineal actual. </summary>
    public Vector3 Velocity { get; private set; } = Vector3.zero;

    /// <summary> Fuerza total resultante de los steerings. </summary>
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

    /// <summary> Asigna el grafo de navegación. </summary>
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

        // 1. Detectar si el entorno es volumétrico (sin normal de superficie definida)
        Vector3 surfaceNormal = Graph.GetNodeNormal(CurrentNode);
        bool isVolumetric = surfaceNormal.sqrMagnitude < 0.0001f;

        Vector3 desiredDirection = SteeringForce;

        if (!isVolumetric)
        {
            // Solo proyectamos sobre el plano si estamos sobre una superficie/terreno 2.5D
            surfaceNormal.Normalize();
            desiredDirection = Vector3.ProjectOnPlane(SteeringForce, surfaceNormal);
        }

        // 2. Evaluar restricción de paredes/obstáculos
        if (EvaluateUnwalkableNodesNormal(transform.position, isVolumetric ? Vector3.zero : surfaceNormal, out Vector3 wallNormal, out float penetrationDepth))
        {
            if (Vector3.Dot(desiredDirection, wallNormal) < 0f)
            {
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, wallNormal);
            }
        }

        // 3. Orientación y rotación volumétrica vs. superficie
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            if (isVolumetric)
            {
                // Rotación 3D completa (Pitch, Yaw, Roll) usando Quaternion.Slerp/RotateTowards
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, transform.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, MaxAngularSpeed * Time.deltaTime);

                Vector3 targetVelocity = transform.forward * MaxSpeed;
                Velocity = Vector3.MoveTowards(Velocity, targetVelocity, MaxForce * Time.deltaTime);
            }
            else
            {
                // Lógica original de rotación en plano 2.5D
                Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal).normalized;
                float angleDifference = Vector3.SignedAngle(currentForward, desiredDirection, surfaceNormal);

                float omega = AngularFrequency;
                float angularAcceleration = omega * omega * angleDifference - 2f * AngularDamping * omega * _currentAngularSpeed;

                _currentAngularSpeed += angularAcceleration * Time.deltaTime;
                _currentAngularSpeed = Mathf.Clamp(_currentAngularSpeed, -MaxAngularSpeed, MaxAngularSpeed);

                transform.Rotate(surfaceNormal, _currentAngularSpeed * Time.deltaTime, Space.World);

                float speedFactor = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(angleDifference) / 120f), TurnTightness);
                Vector3 targetVelocity = transform.forward * (MaxSpeed * speedFactor);
                Velocity = Vector3.MoveTowards(Velocity, targetVelocity, MaxForce * Time.deltaTime);
            }
        }
        else
        {
            Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, MaxForce * Time.deltaTime);
            _currentAngularSpeed = Mathf.MoveTowards(_currentAngularSpeed, 0f, MaxAngularSpeed * Time.deltaTime);
        }

        // 4. Integración y restricciones finales...
        if (wallNormal.sqrMagnitude > 0.0001f)
        {
            float velDot = Vector3.Dot(Velocity, wallNormal);
            if (velDot < 0f) Velocity -= wallNormal * velDot;
            if (penetrationDepth > 0f) transform.position += wallNormal * penetrationDepth;
        }

        if (Velocity.sqrMagnitude > 0.0001f)
        {
            transform.position += Velocity * Time.deltaTime;
        }

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = Velocity;

        Graph.ConstrainPositionAndRotation(ref position, ref velocity, ref rotation);

        transform.SetPositionAndRotation(position, rotation);
        Velocity = velocity;
    }

    /// <summary>
    /// Consulta los nodos interpolables alrededor de la posición.
    /// Soporta tanto entornos 2.5D (proyectando sobre la normal de superficie) como volumétricos puros (3D).
    /// </summary>
    private bool EvaluateUnwalkableNodesNormal(Vector3 position, Vector3 surfaceNormal, out Vector3 wallNormal, out float penetrationDepth)
    {
        wallNormal = Vector3.zero;
        penetrationDepth = 0f;

        int nodeCount = Graph.GetInterpolationNodes(position, _interpolationNodes);
        if (nodeCount <= 0) return false;

        Vector3 accumNormal = Vector3.zero;
        float totalWeight = 0f;
        float maxPenetration = 0f;

        // Si la normal es prácticamente cero, asumimos un entorno volumétrico puro
        bool isVolumetric = surfaceNormal.sqrMagnitude < 0.0001f;

        for (int i = 0; i < nodeCount; i++)
        {
            int node = _interpolationNodes[i];

            // Solo evaluamos los nodos NO caminables
            if (Graph.IsWalkable(node)) continue;

            Vector3 nodePos = Graph.GetNodePosition(node);
            Vector3 diff = position - nodePos;

            // En volumétrico usamos el vector 3D real; en superficies, lo aplanamos sobre el plano del suelo
            Vector3 collisionDiff = isVolumetric ? diff : Vector3.ProjectOnPlane(diff, surfaceNormal);
            float dist = collisionDiff.magnitude;

            if (dist < 0.0001f)
            {
                // Si el agente está exactamente en el mismo punto que el nodo no caminable
                collisionDiff = isVolumetric ? -transform.forward : Vector3.ProjectOnPlane(-transform.forward, surfaceNormal);

                // Fallback de seguridad extrema si no hay un forward válido
                if (collisionDiff.sqrMagnitude < 0.0001f)
                {
                    collisionDiff = isVolumetric ? UnityEngine.Random.onUnitSphere : Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, surfaceNormal);
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

    /// <summary> Calcula la fuerza total acumulada de todos los comportamientos de steering. </summary>
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

    /// <summary> Establece un nuevo nodo objetivo para el agente. </summary>
    public void SetDestination(int targetNode)
    {
        if (TargetNode == targetNode) return;
        TargetNode = targetNode;
        AgentManager.Instance?.Subscribe(this);
    }

    /// <summary> Asigna el proveedor del grafo de navegación. </summary>
    public void SetProvider(NavGraphProvider provider)
    {
        _provider = provider;
        if (provider != null) AssignGraph(provider.Graph);
    }
}