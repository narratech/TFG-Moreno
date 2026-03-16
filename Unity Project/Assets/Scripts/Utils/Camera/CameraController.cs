using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;

    [Header("Rotation")]
    public float mouseSensitivity = 3f;
    public float rotationSmooth = 10f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("Zoom")]
    public float zoomSpeed = 20f;
    public float minDistance = 0f;
    public float maxDistance = 50f;

    [Header("Smoothing")]
    public float positionLerpSpeed = 10f;

    private Vector3 yawPitch; // x = pitch, y = yaw
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yawPitch.x = angles.x; // pitch
        yawPitch.y = angles.y; // yaw

        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void Update()
    {
        HandleRotation();
        HandleMovement();
        HandleZoom();

        ApplyMotion();
    }

    private void ApplyMotion()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionLerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmooth);
    }


    private void HandleMovement()
    {
        Vector2 input = InputManager.Instance.MoveInput;

        Vector3 forward = targetRotation * Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = targetRotation * Vector3.right;
        right.y = 0;
        right.Normalize();

        Vector3 moveDelta = (forward * input.y + right * input.x) * moveSpeed * Time.deltaTime;
        targetPosition += moveDelta;
    }

    private void HandleRotation()
    {
        if (!InputManager.Instance.IsRotating) return;

        Vector2 look = InputManager.Instance.LookInput;

        yawPitch.y += look.x * mouseSensitivity;
        yawPitch.x -= look.y * mouseSensitivity;
        yawPitch.x = Mathf.Clamp(yawPitch.x, minPitch, maxPitch);

        targetRotation = Quaternion.Euler(yawPitch.x, yawPitch.y, 0);
    }

    private void HandleZoom()
    {
        float zoom = InputManager.Instance.ZoomInput;
        if (Mathf.Abs(zoom) < 0.01f) return;

        Vector3 zoomDir = targetRotation * Vector3.forward;
        Vector3 zoomDelta = zoomDir.normalized * zoom * zoomSpeed * Time.deltaTime;
        Vector3 potentialPosition = targetPosition + zoomDelta;

        float distance = Vector3.Distance(potentialPosition, targetPosition); 
        if (distance >= minDistance && distance <= maxDistance)
        {
            targetPosition = potentialPosition;
        }
    }
}
