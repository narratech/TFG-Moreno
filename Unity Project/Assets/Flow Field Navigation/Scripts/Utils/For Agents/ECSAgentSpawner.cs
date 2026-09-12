using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class ECSAgentSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject prefab;

    [Header("Spawn")]
    [SerializeField] private int count = 100;
    [SerializeField] private float radius = 10f;

    [Header("SubScene")]
    [SerializeField] private SubScene subScene;

    [Header("Provider")]
    [SerializeField] private NavGraphProvider provider;

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        if (prefab == null)
        {
            Debug.LogWarning(
                "ECSAgentSpawner: No se ha asignado un prefab.",
                this);

            return;
        }

        if (!prefab.TryGetComponent<NavAgent>(
            out NavAgent agentPrefab))
        {
            Debug.LogWarning(
                "ECSAgentSpawner: El prefab no contiene " +
                "un NavAgent.",
                this);

            return;
        }

#if UNITY_EDITOR

        if (subScene == null)
        {
            Debug.LogWarning(
                "ECSAgentSpawner: No se ha asignado una SubScene.",
                this);

            return;
        }

        Scene editingScene = subScene.EditingScene;

        if (!editingScene.IsValid())
        {
            Debug.LogWarning(
                "ECSAgentSpawner: La EditingScene de la " +
                "SubScene no es válida.",
                this);

            return;
        }

#else

        Debug.LogWarning(
            "ECSAgentSpawner: Este spawner está diseñado " +
            "para ejecutarse desde el Editor.",
            this);

        return;

#endif

        if (provider == null)
        {
            Debug.LogWarning(
                "ECSAgentSpawner: No se ha asignado un Provider.",
                this);

            return;
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

            GameObject obj = Instantiate(
                prefab,
                world,
                Quaternion.LookRotation(
                    transform.forward,
                    up));

            NavAgent authoring =
                obj.GetComponent<NavAgent>();

            SceneManager.MoveGameObjectToScene(
                obj,
                editingScene);

            EditorSceneManager.MarkSceneDirty(
                editingScene);
        }

        Debug.Log(
            $"ECSAgentSpawner: Se han creado {count} agentes " +
            $"en la SubScene.",
            this);
    }


    [ContextMenu("Clear")]
    public void Clear()
    {
#if UNITY_EDITOR

        if (subScene == null)
        {
            Debug.LogWarning(
                "ECSAgentSpawner: No se ha asignado una SubScene.",
                this);

            return;
        }

        Scene editingScene = subScene.EditingScene;

        if (!editingScene.IsValid())
        {
            Debug.LogWarning(
                "ECSAgentSpawner: La EditingScene de la " +
                "SubScene no es válida.",
                this);

            return;
        }

        GameObject[] rootObjects =
            editingScene.GetRootGameObjects();

        int removed = 0;

        foreach (GameObject root in rootObjects)
        {
            NavAgent[] agents =
                root.GetComponentsInChildren<NavAgent>(
                    true);

            foreach (NavAgent agent in agents)
            {
                DestroyImmediate(agent.gameObject);
                removed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(
            editingScene);

        Debug.Log(
            $"ECSAgentSpawner: Se han eliminado {removed} agentes " +
            $"de la SubScene.",
            this);

#else

        Debug.LogWarning(
            "ECSAgentSpawner: Clear() solo está disponible " +
            "en el Editor.",
            this);

#endif
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
