using UnityEngine;
using System.Collections.Generic;

public class ShadowBaker : MonoBehaviour
{

    public OctreeSettings settings;


    /// <summary>
    /// /////////////////////////////////////////////////// ProbePositionBaker
    /// </summary>

    public void BakeProbePositions()
    {
        int width = 256;
        int height = 256;
        int maxCapacity = width * height;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        int childCount = settings.probeRootTransform.childCount;
        Color[] colors = new Color[maxCapacity];

        for (int i = 0; i < maxCapacity; i++)
        {
            if (i < childCount)
            {
                Transform child = settings.probeRootTransform.GetChild(i);
                Vector3 pos = child.position; 
                colors[i] = new Color(pos.x, pos.y, pos.z, 1.0f);
            }
            else
            {
                colors[i] = Color.clear;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.CreateAsset(tex, settings.probePositionSavePath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Position texture successfully saved to: {settings.probePositionSavePath}");
        #endif
    }



    /// <summary>
    /// /////////////////////////////////////////// Closest node Baker
    /// </summary>

    public void BakeClosestProbesTexture()
    {
        #if UNITY_EDITOR
        int childCount = settings.probeRootTransform.childCount;
        if (childCount == 0)
        {
            Debug.LogError("No probes found under probeRootTransform.");
            return;
        }

        Vector3[] probePositions = new Vector3[childCount];
        for (int i = 0; i < childCount; i++)
        {
            probePositions[i] = settings.probeRootTransform.GetChild(i).position;
        }

        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        if (lightmaps == null || lightmaps.Length == 0)
        {
            Debug.LogError("No lightmaps found in the scene! Please bake Unity lighting first.");
            return;
        }

        int lmCount = lightmaps.Length;
        
        int maxWidth = 0;
        int maxHeight = 0;
        for (int i = 0; i < lmCount; i++)
        {
            if (lightmaps[i].lightmapColor != null)
            {
                maxWidth = Mathf.Max(maxWidth, lightmaps[i].lightmapColor.width);
                maxHeight = Mathf.Max(maxHeight, lightmaps[i].lightmapColor.height);
            }
        }

        Texture2DArray textureArray = new Texture2DArray(maxWidth, maxHeight, lmCount, TextureFormat.RGBAFloat, false);
        textureArray.filterMode = FilterMode.Point;
        textureArray.wrapMode = TextureWrapMode.Clamp;

        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();

        // Count total valid renderers for accurate progress tracking
        int totalValidRenderers = 0;
        foreach (var r in renderers)
        {
            if (r.lightmapIndex >= 0 && r.lightmapIndex < lmCount)
                totalValidRenderers++;
        }

        int processedRenderers = 0;
        bool cancelled = false;

        try
        {
            for (int lmIndex = 0; lmIndex < lmCount; lmIndex++)
            {
                Texture2D lmColorTex = lightmaps[lmIndex].lightmapColor;
                if (lmColorTex == null) continue;

                Color[] indexPixels = new Color[maxWidth * maxHeight];
                for (int i = 0; i < indexPixels.Length; i++)
                    indexPixels[i] = Color.clear;

                foreach (var renderer in renderers)
                {
                    if (renderer.lightmapIndex != lmIndex) continue;

                    processedRenderers++;
                    float progress = (float)processedRenderers / Mathf.Max(1, totalValidRenderers);
                    
                    // Display progress bar with cancel capability
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(
                        "Baking Probe Texture Array",
                        $"Processing Lightmap {lmIndex + 1}/{lmCount} (Mesh {processedRenderers}/{totalValidRenderers})",
                        progress))
                    {
                        cancelled = true;
                        break;
                    }

                    MeshFilter mf = renderer.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    Mesh mesh = mf.sharedMesh;
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    Vector2[] uv2 = mesh.uv2;
                    if (uv2 == null || uv2.Length == 0) uv2 = mesh.uv;
                    int[] tris = mesh.triangles;

                    Matrix4x4 localToWorld = renderer.localToWorldMatrix;
                    Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
                    Vector4 scaleOffset = renderer.lightmapScaleOffset;

                    Vector3[] worldVerts = new Vector3[vertices.Length];
                    Vector3[] worldNormals = new Vector3[vertices.Length];
                    Vector2[] uvPixels = new Vector2[vertices.Length];

                    for (int v = 0; v < vertices.Length; v++)
                    {
                        worldVerts[v] = localToWorld.MultiplyPoint(vertices[v]);
                        worldNormals[v] = (normals != null && normals.Length > v) ? normalMatrix.MultiplyVector(normals[v]).normalized : Vector3.up;

                        Vector2 lightmapUV = new Vector2(
                            uv2[v].x * scaleOffset.x + scaleOffset.z,
                            uv2[v].y * scaleOffset.y + scaleOffset.w
                        );
                        uvPixels[v] = new Vector2(lightmapUV.x * maxWidth, lightmapUV.y * maxHeight);
                    }

                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        int i0 = tris[t];
                        int i1 = tris[t + 1];
                        int i2 = tris[t + 2];

                        Vector2 p0 = uvPixels[i0];
                        Vector2 p1 = uvPixels[i1];
                        Vector2 p2 = uvPixels[i2];

                        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), 0, maxWidth - 1);
                        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))), 0, maxWidth - 1);
                        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), 0, maxHeight - 1);
                        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))), 0, maxHeight - 1);

                        for (int y = minY; y <= maxY; y++)
                        {
                            for (int x = minX; x <= maxX; x++)
                            {
                                Vector2 pixelCenter = new Vector2(x + 0.5f, y + 0.5f);
                                if (GetBarycentric(pixelCenter, p0, p1, p2, out float w0, out float w1, out float w2))
                                {
                                    Vector3 worldPos = worldVerts[i0] * w0 + worldVerts[i1] * w1 + worldVerts[i2] * w2;
                                    Vector3 worldNormal = (worldNormals[i0] * w0 + worldNormals[i1] * w1 + worldNormals[i2] * w2).normalized;

                                    int[] closest = Get3ClosestProbes(worldPos, worldNormal, probePositions);

                                    Color c = new Color(
                                        (float)closest[0] / 65535.0f,
                                        (float)closest[1] / 65535.0f,
                                        (float)closest[2] / 65535.0f,
                                        1.0f
                                    );

                                    indexPixels[y * maxWidth + x] = c;
                                }
                            }
                        }
                    }
                }

                if (cancelled) break;
                textureArray.SetPixels(indexPixels, lmIndex);
            }

            if (!cancelled)
            {
                textureArray.Apply();
                UnityEditor.AssetDatabase.CreateAsset(textureArray, settings.probeIndexTextureArraySavePath);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"Probe index Texture2DArray successfully saved to: {settings.probeIndexTextureArraySavePath}");
            }
            else
            {
                Debug.LogWarning("Probe texture baking was cancelled.");
            }
        }
        finally
        {
            // Guaranteed to clear progress bar even if errors occur or baking is cancelled
            UnityEditor.EditorUtility.ClearProgressBar();
        }
        #endif
    }

    private bool GetBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out float w0, out float w1, out float w2)
    {
        Vector2 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector2.Dot(v0, v0);
        float d01 = Vector2.Dot(v0, v1);
        float d11 = Vector2.Dot(v1, v1);
        float d20 = Vector2.Dot(v2, v0);
        float d21 = Vector2.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) < 1e-8f)
        {
            w0 = w1 = w2 = 0;
            return false;
        }
        w1 = (d11 * d20 - d01 * d21) / denom;
        w2 = (d00 * d21 - d01 * d20) / denom;
        w0 = 1.0f - w1 - w2;
        return w0 >= 0f && w1 >= 0f && w2 >= 0f;
    }

    private int[] Get3ClosestProbes(Vector3 pos, Vector3 normal, Vector3[] probePositions)
    {
        List<int> validProbes = new List<int>();
        List<float> validDistances = new List<float>();

        for (int i = 0; i < probePositions.Length; i++)
        {
            Vector3 dirToProbe = probePositions[i] - pos;
            float dist = dirToProbe.magnitude;

            if (dist < 0.0001f)
            {
                validProbes.Add(i);
                validDistances.Add(dist);
                continue;
            }

            Vector3 dirNormalized = dirToProbe / dist;

            if (Vector3.Dot(dirNormalized, normal) <= 0f) continue;

            Ray ray = new Ray(pos + normal * 0.001f, dirNormalized);
            if (Physics.Raycast(ray, out RaycastHit hit, dist - 0.002f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            validProbes.Add(i);
            validDistances.Add(dist);
        }

        for (int i = 0; i < validProbes.Count - 1; i++)
        {
            for (int j = i + 1; j < validProbes.Count; j++)
            {
                if (validDistances[j] < validDistances[i])
                {
                    float tempD = validDistances[i]; validDistances[i] = validDistances[j]; validDistances[j] = tempD;
                    int tempP = validProbes[i]; validProbes[i] = validProbes[j]; validProbes[j] = tempP;
                }
            }
        }

        int best1 = 0, best2 = 0, best3 = 0;

        if (validProbes.Count >= 3)
        {
            best1 = validProbes[0];
            best2 = validProbes[1];
            best3 = validProbes[2];
        }
        else if (validProbes.Count == 2)
        {
            best1 = validProbes[0];
            best2 = validProbes[1];
            best3 = validProbes[1];
        }
        else if (validProbes.Count == 1)
        {
            best1 = validProbes[0];
            best2 = validProbes[0];
            best3 = validProbes[0];
        }
        else
        {
            return GetClosestProbesAbsoluteFallback(pos, probePositions);
        }

        return new int[] { best1, best2, best3 };
    }

    private int[] GetClosestProbesAbsoluteFallback(Vector3 pos, Vector3[] probePositions)
    {
        int best1 = 0, best2 = 0, best3 = 0;
        float dist1 = float.MaxValue, dist2 = float.MaxValue, dist3 = float.MaxValue;

        for (int i = 0; i < probePositions.Length; i++)
        {
            float sqrDist = (probePositions[i] - pos).sqrMagnitude;
            if (sqrDist < dist1)
            {
                best3 = best2; dist3 = dist2;
                best2 = best1; dist2 = dist1;
                best1 = i;    dist1 = sqrDist;
            }
            else if (sqrDist < dist2)
            {
                best3 = best2; dist3 = dist2;
                best2 = i;    dist2 = sqrDist;
            }
            else if (sqrDist < dist3)
            {
                best3 = i;    dist3 = sqrDist;
            }
        }
        return new int[] { best1, best2, best3 };
    }

    public void ApplyLightmapIndices()
    {
        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        int count = 0;
        foreach (var renderer in renderers)
        {
            if (renderer.lightmapIndex < 0) continue;

            renderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat("_LightmapIndex", renderer.lightmapIndex);
            renderer.SetPropertyBlock(propBlock);
            count++;
        }

        Debug.Log($"Successfully applied automatic lightmap indices to {count} renderers.");
    }


    /// <summary>
    /// //////////////////////////////////////////////////// OctreeBaker /////////////////////////////////////////////////////
    /// </summary>

    
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


}