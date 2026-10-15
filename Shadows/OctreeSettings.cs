using UnityEngine;
using System.Collections.Generic;

public class OctreeSettings : MonoBehaviour
{
    public Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
    [Range(1, 7)] public int maxDepth = 4;
    public LayerMask colliderLayerMask = -1;
    public int textureWidth = 512;
    public int textureHeight = 512;

    [Tooltip("Coordinate frame for probe positions. Defaults to this object. Individual probes are stored as vectors, not child objects.")]
    public Transform probeRootTransform;

    public const int MaxProbeCount = 256 * 256;
    [SerializeField, HideInInspector] private List<Vector3> probePositions = new List<Vector3>();
    public IReadOnlyList<Vector3> ProbePositions => probePositions;
    public int ProbeCount => probePositions.Count;

    public Vector3 GetProbeWorldPosition(int index) => ProbeRoot.TransformPoint(probePositions[index]);
    public void SetProbeWorldPosition(int index, Vector3 position) => probePositions[index] = ProbeRoot.InverseTransformPoint(position);
    public void AddProbeWorldPosition(Vector3 position) => probePositions.Add(ProbeRoot.InverseTransformPoint(position));
    public void RemoveProbeAt(int index) => probePositions.RemoveAt(index);
    public void ClearProbes() => probePositions.Clear();

    public Vector3[] GetProbeWorldPositions()
    {
        Vector3[] positions = new Vector3[ProbeCount];
        Matrix4x4 matrix = ProbeRoot.localToWorldMatrix;
        for (int i = 0; i < positions.Length; i++) positions[i] = matrix.MultiplyPoint3x4(probePositions[i]);
        return positions;
    }

    [Tooltip("Path within the Assets folder to save the texture asset.")]
    public string probePositionSavePath = "Assets/MoonlightVRC/Shadows/BakedNodePositions.asset";
    public string octreeSavePath = "Assets/MoonlightVRC/Shadows/OctreeBakedTexture16.asset";

    public string worldProbePositionSavePath = "Assets/MoonlightVRC/Shadows/ProbePositions.asset";
    public string probeIndexTextureArraySavePath = "Assets/MoonlightVRC/Shadows/ProbeIndicesTextureArray.asset";

    [HideInInspector] public GameObject nodePrefab; // Legacy migration data only.
    public Vector3 generatorSize = new Vector3(5f, 5f, 5f);
    public Vector3Int generatorCount = new Vector3Int(3, 3, 3);
    [Range(0.05f, 2f)] public float handleSize = 0.35f;
    public bool showConnections = true;
    [Range(0.5f, 10f)] public float connectionDistance = 3f;
    public bool showLabels;
    public string textureSavePath = "Assets/MoonlightVRC/Shadows/BakedPositionsTexture.asset";
    public bool editNodes;

    [System.Serializable]
    public class OctreePreviewSettings
    {
        public bool enabled;
        public Texture2D bakedTexture;
        [Range(0, 7)] public int displayDepth = 4;
        public bool showOnlyOccupied = true;
    }

    [System.Serializable]
    public class ProbePreviewSettings
    {
        public bool enabled;
        public RenderTexture shadowCalculationTexture;
        public Texture2D probePosTexture;
        public int probeCount = 200;
        public int targetLightIndex;
        public float gizmoRadius = 0.25f;
        public bool liveUpdate = true;
    }

    public OctreePreviewSettings octreePreview = new OctreePreviewSettings();
    public ProbePreviewSettings probePreview = new ProbePreviewSettings();

    [HideInInspector] public bool settingsConsolidated;
    [HideInInspector] public List<int> selectedIndices = new List<int>();
    #if UNITY_EDITOR
    [System.NonSerialized] public bool isDraggingBox;
    [System.NonSerialized] public Vector2 boxStartMousePos;
    [System.NonSerialized] public Vector2 boxCurrentMousePos;
    #endif
    private Texture2D cachedShadowTex;

    public Transform ProbeRoot => probeRootTransform != null ? probeRootTransform : transform;

    private void OnDrawGizmos()
    {
        if (probePreview.enabled) ProbeDebugVisualizer.DrawGizmos(probePreview, ref cachedShadowTex, this);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the bounding box in the scene view when the empty GameObject is selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
