using UnityEngine;

public class AutoMeshCollider : MonoBehaviour
{
    void Start()
    {
        AddMeshCollidersRecursively(transform);
    }

    void AddMeshCollidersRecursively(Transform parent)
    {
        foreach (Transform child in parent)
        {
            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (child.GetComponent<MeshCollider>() == null)
                {
                    MeshCollider col = child.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                    col.convex = false;
                }
            }
            AddMeshCollidersRecursively(child);
        }
    }
}
