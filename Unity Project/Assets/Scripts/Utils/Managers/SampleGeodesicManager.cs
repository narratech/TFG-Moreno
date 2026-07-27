using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SampleGeodesicManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private RectTransform selectionBox;

    [Header("Configuración del Grafo")]
    public NavGraphProvider graphProvider;
    public int targetNode = -1;

    private InputManager input;
    private Camera mainCamera;
    private readonly List<Selectable> selectedUnits = new List<Selectable>();

    public static SampleGeodesicManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        input = InputManager.Instance;
        mainCamera = Camera.main;

        if (selectionBox != null)
            selectionBox.gameObject.SetActive(false);
    }

    private void Update()
    {
        HandleSelectionInput();

        if (input.IsCommanding)
        {
            HandleCommand();
        }
    }

    // --- LÓGICA DE SELECCIÓN ---

    private void HandleSelectionInput()
    {
        // Evitar seleccionar si el click inicia sobre la UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && !input.IsDragging)
            return;

        if (input.IsDragging)
        {
            UpdateSelectionBoxUI();
        }

        // Finalizar selección al soltar el click izquierdo
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (selectionBox != null)
                selectionBox.gameObject.SetActive(false);

            SelectUnitsInBox();
        }
    }

    private void UpdateSelectionBoxUI()
    {
        if (selectionBox == null) return;

        Vector2 startPos = input.DragStartPosition;
        Vector2 currentPos = input.MouseScreenPosition;

        // Umbral mínimo de movimiento para mostrar la caja
        if ((currentPos - startPos).sqrMagnitude < 50f)
            return;

        if (!selectionBox.gameObject.activeSelf)
            selectionBox.gameObject.SetActive(true);

        Vector2 min = Vector2.Min(startPos, currentPos);
        Vector2 max = Vector2.Max(startPos, currentPos);

        selectionBox.position = min;
        selectionBox.sizeDelta = max - min;
    }

    private void SelectUnitsInBox()
    {
        Vector2 startPos = input.DragStartPosition;
        Vector2 endPos = input.MouseScreenPosition;

        // Si fue un click rápido (sin arrastre relevante), hacemos selección por Raycast
        if ((endPos - startPos).sqrMagnitude < 100f)
        {
            HandleSingleClickSelection();
            return;
        }

        if (!input.IsMultiSelect)
        {
            DeselectAll();
        }

        Rect selectionRect = GetScreenRect(startPos, endPos);
        Selectable[] allSelectables = Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);

        foreach (Selectable unit in allSelectables)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);

            // Verificar si la unidad está en pantalla y dentro del rectángulo
            if (screenPos.z > 0 && selectionRect.Contains(screenPos))
            {
                SelectUnit(unit);
            }
        }
    }

    private void HandleSingleClickSelection()
    {
        Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            Selectable selectable = hit.collider.GetComponentInParent<Selectable>();

            if (!input.IsMultiSelect) DeselectAll();

            if (selectable != null)
            {
                SelectUnit(selectable);
            }
        }
        else if (!input.IsMultiSelect)
        {
            DeselectAll();
        }
    }

    // --- LÓGICA DE NAVEGACIÓN Y COMANDOS ---

    private void HandleCommand()
    {
        if (selectedUnits.Count == 0) return;

        Ray ray = mainCamera.ScreenPointToRay(input.MouseScreenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            OnClickGround(hit.point);
        }
    }

    private void OnClickGround(Vector3 pos)
    {
        int destination = graphProvider.Graph.GetClosestNode(pos);
        if (destination == -1 || !graphProvider.Graph.IsWalkable(destination))
        {
            targetNode = -1;
            return;
        }

        if (!FlowFieldManager.Instance.TryGetRoute(graphProvider.Graph, destination))
        {
            FlowFieldManager.Instance.RegisterRoute(graphProvider.Graph, destination);
        }
        targetNode = destination;

        NavAgent[] activeAgents = new NavAgent[selectedUnits.Count];

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            NavAgent agent = selectedUnits[i].Agent;
            if (agent != null)
            {
                agent.SetDestination(targetNode);
                activeAgents[i] = agent;
            }
        }

        FormationGenerator.GenerateAndApply(FormationType.Circle, 10f, activeAgents);
    }

    // --- GESTIÓN DE SELECCIÓN DE UNIDADES ---

    public void SelectUnit(Selectable unit)
    {
        if (!selectedUnits.Contains(unit))
        {
            selectedUnits.Add(unit);
            unit.SetSelected(true);
        }
    }

    public void DeselectAll()
    {
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            if (selectedUnits[i] != null)
                selectedUnits[i].SetSelected(false);
        }
        selectedUnits.Clear();
    }

    private Rect GetScreenRect(Vector2 screenPos1, Vector2 screenPos2)
    {
        Vector2 min = Vector2.Min(screenPos1, screenPos2);
        Vector2 max = Vector2.Max(screenPos1, screenPos2);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}