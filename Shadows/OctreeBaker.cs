#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class OctreeBaker
{
    private readonly OctreeSettings settings;

    public OctreeBaker(OctreeSettings settings)
    {
        this.settings = settings;
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

    public void BakeOctree()
    {
        if (settings == null) return;

        List<OctreeNode> nodes = new List<OctreeNode>();
        OctreeNode root = new OctreeNode(settings.bounds);
        nodes.Add(root);

        BuildOctreeRecursive(root, 0, settings.maxDepth, nodes);

        Debug.Log($"Octree built with {nodes.Count} nodes.");

        if (nodes.Count > 65535)
        {
            Debug.LogError("Octree node count exceeds 65,535 limit for 16-bit storage!");
            return;
        }

        int totalNodes = nodes.Count;
        int requiredPixels = totalNodes * 3;
        int targetWidth = settings.textureWidth;
        int targetHeight = settings.textureHeight;
        
        if (targetWidth * targetHeight < requiredPixels)
        {
            int side = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(requiredPixels)));
            targetWidth = Mathf.Max(targetWidth, side);
            targetHeight = Mathf.Max(targetHeight, side);
        }

        Texture2D tex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA64, false, true);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        ushort[] pixelData = new ushort[targetWidth * targetHeight * 4];
        System.Array.Clear(pixelData, 0, pixelData.Length);

        for (int i = 0; i < totalNodes; i++)
        {
            OctreeNode node = nodes[i];
            int baseIndex = i * 12;

            pixelData[baseIndex + 0] = (ushort)(node.isOccupied ? 1 : 0);
            pixelData[baseIndex + 1] = (ushort)(node.isLeaf ? 1 : 0);
            pixelData[baseIndex + 2] = 0;
            pixelData[baseIndex + 3] = 0;

            pixelData[baseIndex + 4] = node.childIndices[0] != -1 ? (ushort)node.childIndices[0] : (ushort)0;
            pixelData[baseIndex + 5] = node.childIndices[1] != -1 ? (ushort)node.childIndices[1] : (ushort)0;
            pixelData[baseIndex + 6] = node.childIndices[2] != -1 ? (ushort)node.childIndices[2] : (ushort)0;
            pixelData[baseIndex + 7] = node.childIndices[3] != -1 ? (ushort)node.childIndices[3] : (ushort)0;

            pixelData[baseIndex + 8]  = node.childIndices[4] != -1 ? (ushort)node.childIndices[4] : (ushort)0;
            pixelData[baseIndex + 9]  = node.childIndices[5] != -1 ? (ushort)node.childIndices[5] : (ushort)0;
            pixelData[baseIndex + 10] = node.childIndices[6] != -1 ? (ushort)node.childIndices[6] : (ushort)0;
            pixelData[baseIndex + 11] = node.childIndices[7] != -1 ? (ushort)node.childIndices[7] : (ushort)0;
        }

        tex.SetPixelData(pixelData, 0);
        tex.Apply(false, false);

        string path = settings.octreeSavePath;
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

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        Debug.Log($"16-bit Octree texture successfully baked and saved to {path} with {totalNodes} nodes.");
    }

    private void BuildOctreeRecursive(OctreeNode node, int currentDepth, int maxD, List<OctreeNode> nodeList)
    {
        Vector3 center = node.bounds.center;
        Vector3 halfExtents = node.bounds.extents * 0.99f;
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, settings.colliderLayerMask);

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

                for (int i = 0; i < 8; i++)
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


    
    public void BakeProbePositions()
    {

        int width = 256;
        int height = 256;
        int maxCapacity = width * height;

        // Create an RGBAFloat texture to preserve high-precision coordinates
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        int childCount = settings.probeRootTransform.childCount;
        Debug.Log($"Baking {childCount} children positions (Max capacity: {maxCapacity}).");

        Color[] colors = new Color[maxCapacity];

        for (int i = 0; i < maxCapacity; i++)
        {
            if (i < childCount)
            {
                Transform child = settings.probeRootTransform.GetChild(i);
                // Use localPosition (or child.position for world space)
                Vector3 pos = child.localPosition; 
                colors[i] = new Color(pos.x, pos.y, pos.z, 1.0f);
            }
            else
            {
                // Fill remaining pixels with zero if there are fewer than 65,536 children
                colors[i] = Color.clear;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        // Save the texture asset in the Unity Editor
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.CreateAsset(tex, settings.probePositionSavePath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Position texture successfully saved to: {settings.probePositionSavePath}");
        #endif
    }
}
#endif
