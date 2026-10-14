using UnityEngine;

public class OctreeSettings : MonoBehaviour
{
    public Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
    [Range(1, 7)] public int maxDepth = 4;
    public LayerMask colliderLayerMask = -1;
    public string textureName = "OctreeBakedTexture16";
    public int textureWidth = 512;
    public int textureHeight = 512;

    private void OnDrawGizmosSelected()
    {
        // Visualize the bounding box in the scene view when the empty GameObject is selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}