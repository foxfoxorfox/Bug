using UnityEngine;

public class ObjectControl : MonoBehaviour
{
    public bool meshEnabled = true;
    public bool colliderEnabled = true;
    public Transform bugPoint;
    public Transform goalPoint;
    public void ApplySettings()
    {
        MeshRenderer[] renderers =
            GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            renderer.enabled = meshEnabled;
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            collider.enabled = colliderEnabled;
        }
    }
}