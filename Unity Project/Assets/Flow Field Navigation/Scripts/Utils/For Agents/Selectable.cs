using UnityEngine;

[RequireComponent(typeof(NavAgent))]
public class Selectable : MonoBehaviour
{
    [Header("Visual Feedback (Opcional)")]
    [SerializeField] private GameObject selectionIndicator; // Objeto visual (ej. un círculo a los pies)

    public NavAgent Agent { get; private set; }
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        Agent = GetComponent<NavAgent>();
        if (selectionIndicator != null)
            selectionIndicator.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(selected);
        }
    }
}