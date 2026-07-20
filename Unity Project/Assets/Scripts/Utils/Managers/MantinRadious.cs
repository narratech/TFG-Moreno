using UnityEngine;

public class MantinRadious : MonoBehaviour
{
    public Vector3 centre;
    public float radius;
    void Update()
    {
        Vector3 dir = transform.position - centre;
        Vector3 newDir = dir.normalized * radius;
        transform.position = newDir + centre;
        //transform.up = newDir.normalized;
    }
}
