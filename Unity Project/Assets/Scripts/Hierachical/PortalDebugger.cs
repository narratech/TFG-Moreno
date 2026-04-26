using UnityEngine;
using UnityEngine.InputSystem;

public class PortalDebugger : MonoBehaviour
{
    Grid2DProvider _provider;

    private void Awake()
    {
        _provider = GetComponent<Grid2DProvider>();
    }

    private void OnDrawGizmosSelected()
    {
        if (_provider == null)
            return;
        if (FlowFieldManager.Instance == null)
            return;
        if (!FlowFieldManager.Instance.TryGetContext(_provider.Graph))
            return;

        Gizmos.color = Color.magenta;
        foreach (var portal in FlowFieldManager.Instance.GetPortalGraph(_provider.Graph).GetAllPortals())
        {
            Vector3 midle = (portal.PositionA + portal.PositionB) / 2;

            Gizmos.DrawSphere(midle, 0.5f);
        }
    }
}