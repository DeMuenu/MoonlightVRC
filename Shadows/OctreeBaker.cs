using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class OctreeBakerWindow : EditorWindow
{
    private Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
    private int maxDepth = 4;
    private LayerMask colliderLayerMask = -1;
    private string textureName = "OctreeBakedTexture16";
    private int textureWidth = 512;
    private int textureHeight = 512;

    [MenuItem("Tools/Octree Baker (16-bit)")]
    public static void ShowWindow()
    {
        GetWindow<OctreeBakerWindow>("Octree Baker 16-bit");
    }


    private void OnGUI()
    {
        GUILayout.Label("Octree to 16-bit Texture Baker (RGBA64)", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        bounds = EditorGUILayout.BoundsField("Octree Bounds", bounds);
        maxDepth = EditorGUILayout.IntSlider("Max Depth", maxDepth, 1, 7);
        colliderLayerMask = LayerMaskField("Collider Layer Mask", colliderLayerMask);
        
        EditorGUILayout.Space();
        textureName = EditorGUILayout.TextField("Texture Asset Name", textureName);
        textureWidth = EditorGUILayout.IntField("Texture Width", textureWidth);
        textureHeight = EditorGUILayout.IntField("Texture Height", textureHeight);

        EditorGUILayout.Space();
        if (GUILayout.Button("Calculate and Bake Octree Texture"))
        {
            BakeOctree();
        }
    }

    private LayerMask LayerMaskField(string label, LayerMask mask)
    {
        List<string> layers = new List<string>();
        List<int> layerValues = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
                layerValues.Add(1 << i);
            }
        }
        int originalMask = mask.value;
        int retMask = EditorGUILayout.MaskField(label, originalMask, layers.ToArray());
        return retMask;
    }

    private class OctreeNode
    {
        public Bounds bounds;
        public bool isOccupied;
        public bool isLeaf;
        public int[] childIndices = new int[8];

        public OctreeNode(Bounds b)
        {
            bounds = b;
            for (int i = 0; i < 8; i++) childIndices[i] = -1;
        }
    }

    private void BakeOctree()
    {
        //Build Octree
        List<OctreeNode> nodes = new List<OctreeNode>();
        OctreeNode root = new OctreeNode(bounds);
        nodes.Add(root);

        BuildOctreeRecursive(root, 0, maxDepth, nodes);

        Debug.Log($"Octree built with {nodes.Count} nodes.");

        if (nodes.Count > 65535)
        {
            Debug.LogError("Octree node count exceeds 65,535 limit for 16-bit storage!");
            return;
        }

        //Serialize Octree nodes into a 16-bit RGBA64 Texture 
        int totalNodes = nodes.Count;
        int requiredPixels = totalNodes * 3;
        
        if (textureWidth * textureHeight < requiredPixels)
        {
            int side = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(requiredPixels)));
            textureWidth = Mathf.Max(textureWidth, side);
            textureHeight = Mathf.Max(textureHeight, side);
            Debug.Log($"Resized texture dimensions to {textureWidth}x{textureHeight} to fit {totalNodes} nodes ({requiredPixels} pixels).");
        }

        // Create texture with TextureFormat.RGBA64 (16-bits unsigned per channel)
        Texture2D tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA64, false, true);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        ushort[] pixelData = new ushort[textureWidth * textureHeight * 4];
        for (int i = 0; i < pixelData.Length; i++)
        {
            pixelData[i] = 0;
        }

        for (int i = 0; i < totalNodes; i++)
        {
            OctreeNode node = nodes[i];
            int baseIndex = i * 12; // 3 pixels * 4 channels = 12 ushorts per node

            // Pixel 0: Node Data
            pixelData[baseIndex + 0] = (ushort)(node.isOccupied ? 1 : 0); // Red: Occupied
            pixelData[baseIndex + 1] = (ushort)(node.isLeaf ? 1 : 0);     // Green: Leaf
            pixelData[baseIndex + 2] = 0;                                 // Blue: Unused
            pixelData[baseIndex + 3] = 0;                                 // Alpha: Unused

            // Pixel 1: Sub-nodes 0 to 3 (R, G, B, A)
            // 0 represents no child exists (root is index 0, children start at index 1)
            pixelData[baseIndex + 4] = node.childIndices[0] != -1 ? (ushort)node.childIndices[0] : (ushort)0;
            pixelData[baseIndex + 5] = node.childIndices[1] != -1 ? (ushort)node.childIndices[1] : (ushort)0;
            pixelData[baseIndex + 6] = node.childIndices[2] != -1 ? (ushort)node.childIndices[2] : (ushort)0;
            pixelData[baseIndex + 7] = node.childIndices[3] != -1 ? (ushort)node.childIndices[3] : (ushort)0;

            // Pixel 2: Sub-nodes 4 to 7 (R, G, B, A)
            pixelData[baseIndex + 8]  = node.childIndices[4] != -1 ? (ushort)node.childIndices[4] : (ushort)0;
            pixelData[baseIndex + 9]  = node.childIndices[5] != -1 ? (ushort)node.childIndices[5] : (ushort)0;
            pixelData[baseIndex + 10] = node.childIndices[6] != -1 ? (ushort)node.childIndices[6] : (ushort)0;
            pixelData[baseIndex + 11] = node.childIndices[7] != -1 ? (ushort)node.childIndices[7] : (ushort)0;
        }

        // Push raw ushort array to texture data
        tex.SetPixelData(pixelData, 0);
        tex.Apply(false, false);

        // 3. Save Texture Asset directly as a native Unity asset
        string path = $"Assets/MoonlightVRC/Shadows/{textureName}.asset";
        
        Texture2D existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existingTex != null)
        {
            EditorUtility.CopySerialized(tex, existingTex);
            AssetDatabase.SaveAssets();
        }
        else
        {
            AssetDatabase.CreateAsset(tex, path);
            AssetDatabase.SaveAssets();
        }
        
        AssetDatabase.Refresh();

        // Configure Import Settings
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        Debug.Log($"16-bit Octree texture successfully baked and saved to {path} with {totalNodes} nodes.");
    }

    private void BuildOctreeRecursive(OctreeNode node, int currentDepth, int maxD, List<OctreeNode> nodeList)
    {
        Vector3 center = node.bounds.center;
        Vector3 halfExtents = node.bounds.extents * 0.99f;
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, colliderLayerMask);

        if (colliders.Length > 0)
        {
            node.isOccupied = true;

            if (currentDepth < maxD)
            {
                node.isLeaf = false;
                Vector3 size = node.bounds.size;
                Vector3 childSize = size * 0.5f;
                Vector3 childExtents = childSize * 0.5f;
                Vector3 min = node.bounds.min;

                for (int i = 0; i < 0x8; i++) // 8 child nodes
                {
                    Vector3 childCenter = min + new Vector3(
                        (i & 1) == 0 ? childExtents.x : childExtents.x + childSize.x,
                        (i & 2) == 0 ? childExtents.y : childExtents.y + childSize.y,
                        (i & 4) == 0 ? childExtents.z : childExtents.z + childSize.z
                    );

                    Bounds childBounds = new Bounds(childCenter, childSize);
                    OctreeNode childNode = new OctreeNode(childBounds);
                    
                    int childIndex = nodeList.Count;
                    nodeList.Add(childNode);
                    node.childIndices[i] = childIndex;

                    BuildOctreeRecursive(childNode, currentDepth + 1, maxD, nodeList);
                }
            }
            else
            {
                node.isLeaf = true;
            }
        }
        else
        {
            node.isOccupied = false;
            node.isLeaf = true;
        }
    }
}