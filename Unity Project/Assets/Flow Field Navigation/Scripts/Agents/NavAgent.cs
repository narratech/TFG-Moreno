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

        // 1. Obtener la normal del plano de movimiento actual
        Vector3 surfaceNormal = Graph.GetNodeNormal(CurrentNode);
        if (surfaceNormal.sqrMagnitude < 0.0001f) surfaceNormal = Vector3.up;
        else surfaceNormal.Normalize();

        Vector3 desiredDirection = Vector3.ProjectOnPlane(SteeringForce, surfaceNormal);

        // 2. Evaluar colisión/restricción con nodos interpolables no caminables
        if (EvaluateUnwalkableNodesNormal(transform.position, surfaceNormal, out Vector3 wallNormal, out float penetrationDepth))
        {
            // Eliminar la componente que penetra el obstáculo en la fuerza deseada
            if (Vector3.Dot(desiredDirection, wallNormal) < 0f)
            {
                desiredDirection = Vector3.ProjectOnPlane(desiredDirection, wallNormal);
            }
        }

        // 3. Orientación y rotación
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            desiredDirection.Normalize();

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (currentForward.sqrMagnitude < 0.0001f) currentForward = desiredDirection;
            else currentForward.Normalize();

            float angleDifference = Vector3.SignedAngle(currentForward, desiredDirection, surfaceNormal);

            float omega = AngularFrequency;
            float angularAcceleration = omega * omega * angleDifference - 2f * AngularDamping * omega * _currentAngularSpeed;

            _currentAngularSpeed += angularAcceleration * Time.deltaTime;
            _currentAngularSpeed = Mathf.Clamp(_currentAngularSpeed, -MaxAngularSpeed, MaxAngularSpeed);

            transform.Rotate(surfaceNormal, _currentAngularSpeed * Time.deltaTime, Space.World);

            float angle = Mathf.Abs(angleDifference);
            float speedFactor = Mathf.Clamp01(1f - angle / 120f);
            speedFactor = Mathf.Pow(speedFactor, TurnTightness);

            Vector3 movementForward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (movementForward.sqrMagnitude > 0.0001f) movementForward.Normalize();
            else movementForward = desiredDirection;

            Vector3 targetVelocity = movementForward * (MaxSpeed * speedFactor);
            Velocity = Vector3.MoveTowards(Velocity, targetVelocity, MaxForce * Time.deltaTime);
        }
        else
        {
            Velocity = Vector3.MoveTowards(Velocity, Vector3.zero, MaxForce * Time.deltaTime);
            _currentAngularSpeed = Mathf.MoveTowards(_currentAngularSpeed, 0f, MaxAngularSpeed * Time.deltaTime);
        }

        // 4. Aplicar restricción de normal de pared sobre la VELOCIDAD actual
        if (wallNormal.sqrMagnitude > 0.0001f)
        {
            // Si la velocidad va en dirección a la pared, eliminamos la componente perpendicular (normal)
            float velDot = Vector3.Dot(Velocity, wallNormal);
            if (velDot < 0f)
            {
                Velocity -= wallNormal * velDot; // Proyección tangencial pura
            }

            // Corrección sutil de posición si el agente sobrepasa la tolerancia del borde
            if (penetrationDepth > 0f)
            {
                transform.position += wallNormal * penetrationDepth;
            }
        }

        // 5. Aplicar integración de movimiento
        if (Velocity.sqrMagnitude > 0.0001f)
        {
            transform.position += Velocity * Time.deltaTime;
        }

        // 6. Restricciones finales del grafo
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 velocity = Velocity;

        Graph.ConstrainPositionAndRotation(ref position, ref velocity, ref rotation);

        transform.SetPositionAndRotation(position, rotation);
        Velocity = velocity;
    }

    /// <summary>
    /// Consulta los nodos interpolables alrededor de la posición.
    /// Si hay nodos no caminables, calcula la normal media ponderada del obstáculo hacia la posición.
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

        for (int i = 0; i < nodeCount; i++)
        {
            int node = _interpolationNodes[i];

            // Solo evaluamos los nodos NO caminables
            if (Graph.IsWalkable(node)) continue;

            Vector3 nodePos = Graph.GetNodePosition(node);
            Vector3 diff = position - nodePos;

            // Proyectamos sobre el plano de la superficie para calcular la dirección tangencial en 2.5D/3D
            Vector3 planeDiff = Vector3.ProjectOnPlane(diff, surfaceNormal);
            float dist = planeDiff.magnitude;

            if (dist < 0.0001f)
            {
                // Si el agente está exactamente sobre el nodo no caminable, empujar según la superficie
                planeDiff = -transform.forward;
                dist = 0.01f;
            }

            Vector3 dirFromObstacle = planeDiff / dist;
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