using UnityEngine;

public class OctreeSettings : MonoBehaviour
{
    public Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
    [Range(1, 7)] public int maxDepth = 4;
    public LayerMask colliderLayerMask = -1;
    public int textureWidth = 512;
    public int textureHeight = 512;

    [Tooltip("The parent transform whose children will be baked. Defaults to this object if left empty.")]
    public Transform probeRootTransform;

    [Tooltip("Path within the Assets folder to save the texture asset.")]
    public string probePositionSavePath = "Assets/MoonlightVRC/Shadows/BakedNodePositions.asset";
    public string octreeSavePath = "Assets/MoonlightVRC/Shadows/OctreeBakedTexture16.asset";

    private void OnDrawGizmosSelected()
    {
        // Visualize the bounding box in the scene view when the empty GameObject is selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}