using UnityEngine;

public class AgentSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject prefab;

    [Header("Spawn")]
    [SerializeField] private int count = 100;
    [SerializeField] private float radius = 10f;

    [Header("Container")]
    [SerializeField] private Transform container;

    [Header("Prefab")]
    [SerializeField] private MonoBehaviour provider;

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        if (prefab == null)
            return;

        if (container == null)
        {
            GameObject go = GameObject.Find("Container");

            if (go == null)
                go = new GameObject("Container");

            container = go.transform;
        }

        Vector3 center = transform.position;
        Vector3 up = transform.up;

        Quaternion rotation =
            Quaternion.FromToRotation(
                Vector3.up,
                up);

        for (int i = 0; i < count; i++)
        {
            Vector2 p =
                Random.insideUnitCircle * radius;

            Vector3 world =
                center +
                rotation *
                new Vector3(
                    p.x,
                    0f,
                    p.y);

            Instantiate(
                prefab,
                world,
                Quaternion.LookRotation(transform.forward, up),
                container).GetComponent<NavAgent>().provider = provider;
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(container.GetChild(i).gameObject);
            else
#endif
                Destroy(container.GetChild(i).gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Quaternion rotation =
            Quaternion.FromToRotation(
                Vector3.up,
                transform.up);

        const int segments = 64;

        Vector3 prev =
            transform.position +
            rotation *
            new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle =
                i * Mathf.PI * 2f / segments;

            Vector3 next =
                transform.position +
                rotation *
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

            Gizmos.DrawLine(prev, next);

            prev = next;
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(
            transform.position,
            transform.up * radius * 0.5f);
    }
}