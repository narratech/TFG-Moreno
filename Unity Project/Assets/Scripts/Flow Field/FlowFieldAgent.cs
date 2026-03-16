using UnityEngine;
using UnityEngine.AI;

public class FlowFieldAgent : MonoBehaviour
{
    public FlowFieldGrid grid;

    private NavMeshAgent navMeshAgent;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent component not found on FlowFieldAgent");
        }

        navMeshAgent.updatePosition = true;
        navMeshAgent.updateRotation = false; // tú controlas la rotación
        navMeshAgent.acceleration = 50f;     // importante para suavidad
        navMeshAgent.autoBraking = false;

    }

    void Update()
    {
        if (grid == null) return;

        FlowFieldCell cell = grid.GetCellAtWorldPos(transform.position);

        Vector3 desiredDir = new Vector3(cell.direction.x, 0, cell.direction.y);
        if (grid.IsDestination(cell))
        {
            // Si llegamos a la celda destino, ir hacia el centro de la celda
            desiredDir = (grid.DestinationWorldCentre() - transform.position);
        }

        if (desiredDir.sqrMagnitude < 0.2f)
        {
            // Sin dirección deseada, detenerse suavemente
            navMeshAgent.velocity = Vector3.Lerp(
                navMeshAgent.velocity,
                Vector3.zero,
                Time.deltaTime * navMeshAgent.acceleration
            );
            return;
        }

        desiredDir.Normalize();

        // --- VELOCIDAD ACTUAL ---
        Vector3 currentVelocity = navMeshAgent.velocity;
        Vector3 currentDir = currentVelocity.sqrMagnitude > 0.01f
            ? currentVelocity.normalized
            : transform.forward;

        // --- VELOCIDAD DESEADA ---
        Vector3 desiredVelocity = desiredDir * navMeshAgent.speed;

        // --- ÁNGULO ENTRE VELOCIDADES ---
        float angle = Vector3.Angle(currentDir, desiredDir); // 0..180

        // 0 = alineado → rotación lenta
        // 180 = opuesto → rotación rápida
        float rotationFactor = Mathf.InverseLerp(0f, 180f, angle);

        // --- ROTACIÓN DINÁMICA ---
        float dynamicAngularSpeed = Mathf.Lerp(
            0, // mínimo
            navMeshAgent.angularSpeed,    // máximo
            rotationFactor
        );

        Quaternion targetRotation = Quaternion.LookRotation(desiredDir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            dynamicAngularSpeed * Time.deltaTime
        );

        // --- ALIGNMENT PARA AVANCE ---
        float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, desiredDir));

        // --- AVANCE SOLO HACIA ADELANTE ---
        Vector3 forwardVelocity = transform.forward * navMeshAgent.speed * alignment;

        // --- SUAVIZADO DE VELOCIDAD (INERCIA) ---
        navMeshAgent.velocity = Vector3.Lerp(
            navMeshAgent.velocity,
            forwardVelocity,
            Time.deltaTime * navMeshAgent.acceleration
        );
    }



}
