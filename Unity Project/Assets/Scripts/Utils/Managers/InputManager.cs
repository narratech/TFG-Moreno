using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestiona todos los inputs del jugador para cámara y movimiento.
/// Singleton accesible desde cualquier parte mediante InputManager.Instance.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Input Asset")]
    [Tooltip("Asigna el InputActionAsset que contiene las acciones de cámara.")]
    public InputActionAsset input;

    // Valores de input expuestos
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public float ZoomInput { get; private set; }
    public bool IsRotating { get; private set; }

    public Vector2 MouseScreenPosition { get; private set; }
    public bool IsSelecting { get; private set; }
    public bool IsCommanding { get; private set; }

    public bool IsMultiSelect => Keyboard.current != null &&
                                (Keyboard.current.leftShiftKey.isPressed ||
                                 Keyboard.current.rightShiftKey.isPressed);

    public Vector3 DragStartPosition { get; private set; }

    // Indica si se está manteniendo presionado el botón de selección
    public bool IsDragging { get; private set; }

    // Referencias internas a acciones
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction zoomAction;
    private InputAction rotateAction;

    private InputAction mousePositionAction;
    private InputAction selectAction;
    private InputAction commandAction;

    private void Awake()
    {
        // Configurar singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (input == null)
        {
            Debug.LogError("Input Action Asset no asignado en InputManager!");
            return;
        }

        // Activar el Action Map de cámara
        var cameraMap = input.FindActionMap("RTS Game");
        cameraMap.Enable();

        // Obtener referencias a cada acción
        moveAction = cameraMap.FindAction("Move");
        lookAction = cameraMap.FindAction("Look");
        zoomAction = cameraMap.FindAction("Zoom");
        rotateAction = cameraMap.FindAction("Rotate");
        mousePositionAction = cameraMap.FindAction("MousePosition");
        selectAction = cameraMap.FindAction("Select");
        commandAction = cameraMap.FindAction("Command");
    }

    private void OnDisable()
    {
        // Desactivar el Action Map al deshabilitar el objeto
        if (input != null)
            input.FindActionMap("Camera")?.Disable();
    }

    private void Update()
    {
        // Leer inputs cada frame
        if (moveAction != null) MoveInput = moveAction.ReadValue<Vector2>();

        if (lookAction != null) LookInput = lookAction.ReadValue<Vector2>();

        if (zoomAction != null) ZoomInput = zoomAction.ReadValue<float>();

        if (rotateAction != null) IsRotating = rotateAction.IsPressed();

        if (mousePositionAction != null) MouseScreenPosition = mousePositionAction.ReadValue<Vector2>();

        if (selectAction != null) IsSelecting = selectAction.WasPressedThisFrame();

        if (commandAction != null) IsCommanding = commandAction.WasPressedThisFrame();

        if (Mouse.current == null) return;

        // Detectar inicio de click
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            DragStartPosition = Mouse.current.position.ReadValue();
            IsDragging = true;
        }

        // Detectar cuando se suelta el click
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            IsDragging = false;
        }
    }
}
