using UnityEngine;
using System.Collections.Generic;

public class UltimateNodePlacer : MonoBehaviour
{
    [Tooltip("Optional prefab to instantiate for each node. If empty, an empty GameObject will be created.")]
    public GameObject nodePrefab;

    [Header("Generator Settings")]
    public Vector3 generatorSize = new Vector3(5f, 5f, 5f);
    public Vector3Int generatorCount = new Vector3Int(3, 3, 3);

    [Header("Visualization Settings")]
    public float handleSize = 0.35f;
    public bool showConnections = true;
    public float connectionDistance = 3.0f;
    public bool showLabels = false;

    [Header("Texture Bake Settings")]
    public string textureSavePath = "Assets/MoonlightVRC/Shadows/BakedPositionsTexture.asset";

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
