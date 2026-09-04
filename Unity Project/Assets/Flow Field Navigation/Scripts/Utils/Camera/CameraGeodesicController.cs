using UnityEngine;

public class CameraGeodesicController : MonoBehaviour
{
    [Header("Planet")]
    [SerializeField] private Transform centre;
    [SerializeField] private float minRadious = 500f;
    [SerializeField] private float maxRadious = 500f;

    [Header("Movement")]
    [SerializeField] private float keyboardSpeed = 60f;
    [SerializeField] private float mouseSpeed = 0.2f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 150f;
    [SerializeField] private float minDistance = 20f;
    [SerializeField] private float maxDistance = 1000f;

    [Header("Smoothing")]
    [SerializeField] private float smooth = 10f;

    Quaternion targetOrbit;
    Quaternion currentOrbit;

    float targetDistance;
    float currentDistance;

    void Start()
    {
        Vector3 dir = (transform.position - centre.position).normalized;

        targetOrbit = Quaternion.FromToRotation(Vector3.forward, dir);
        currentOrbit = targetOrbit;

        targetDistance = Vector3.Distance(transform.position, centre.position);
        currentDistance = targetDistance;
    }

    void Update()
    {
        HandleKeyboard();
        HandleMouse();
        HandleZoom();

        currentOrbit = Quaternion.Slerp(
            currentOrbit,
            targetOrbit,
            Time.deltaTime * smooth);

        currentDistance = Mathf.Lerp(
            currentDistance,
            targetDistance,
            Time.deltaTime * smooth);

        Vector3 position =
            centre.position +
            currentOrbit * Vector3.forward * currentDistance;

        transform.position = position;
        transform.LookAt(centre.position);
    }

    void HandleKeyboard()
    {
        Vector2 move = InputManager.Instance.MoveInput;

        if (move.sqrMagnitude < 0.0001f)
            return;

        Quaternion yaw =
            Quaternion.AngleAxis(
                move.x * keyboardSpeed * Time.deltaTime,
                Vector3.up);

        Vector3 right = targetOrbit * Vector3.right;

        Quaternion pitch =
            Quaternion.AngleAxis(
                -move.y * keyboardSpeed * Time.deltaTime,
                right);

        targetOrbit = pitch * yaw * targetOrbit;
    }

    void HandleMouse()
    {
        if (!InputManager.Instance.IsRotating)
            return;

        Vector2 look = InputManager.Instance.LookInput;

        Quaternion yaw =
            Quaternion.AngleAxis(
                look.x * mouseSpeed * currentDistance * 0.001f,
                Vector3.up);

        Vector3 right = targetOrbit * Vector3.right;

        Quaternion pitch =
            Quaternion.AngleAxis(
                -look.y * mouseSpeed * currentDistance * 0.001f,
                right);

        targetOrbit = pitch * yaw * targetOrbit;
    }

    void HandleZoom()
    {
        float zoom = InputManager.Instance.ZoomInput;

        if (Mathf.Abs(zoom) < 0.01f)
            return;

        float newDist = zoomSpeed * 100 * Time.deltaTime;

        newDist = Mathf.Clamp(
            newDist,
            minDistance,
            maxDistance) * zoom;

        targetDistance -= newDist;

        if (targetDistance > maxRadious) targetDistance = maxRadious;
        if (targetDistance < minRadious) targetDistance = minRadious;
    }
}