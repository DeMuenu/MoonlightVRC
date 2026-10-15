using UnityEngine;
using System.Collections.Generic;

public class UltimateNodePlacer : MonoBehaviour
{
    // Legacy serialized values are kept for migration into OctreeSettings.
    [HideInInspector]
    public GameObject nodePrefab;

    [HideInInspector] public Vector3 generatorSize = new Vector3(5f, 5f, 5f);
    [HideInInspector] public Vector3Int generatorCount = new Vector3Int(3, 3, 3);

    [HideInInspector] public float handleSize = 0.35f;
    [HideInInspector] public bool showConnections = true;
    [HideInInspector] public float connectionDistance = 3.0f;
    [HideInInspector] public bool showLabels = false;

    [HideInInspector] public string textureSavePath = "Assets/MoonlightVRC/Shadows/BakedPositionsTexture.asset";

    // Internal selection state tracking child indices
    [HideInInspector]
    public List<int> selectedIndices = new List<int>();

    // Marquee selection rect tracking
    #if UNITY_EDITOR
    [HideInInspector] public bool isDraggingBox = false;
    [HideInInspector] public Vector2 boxStartMousePos;
    [HideInInspector] public Vector2 boxCurrentMousePos;
    #endif
}
