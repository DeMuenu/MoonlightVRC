using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions.Comparers;

public class ProbeBaker : MonoBehaviour
{
    [System.Serializable]
    public class BakeSettings
    {
        public Transform probeRootTransform;
        public string probePositionSavePath = "Assets/ProbePositions.asset";
        public string probeIndexTextureArraySavePath = "Assets/MoonlightVRC/Shadows/ProbeIndicesTextureArray.asset";
    }

    public BakeSettings settings;

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

        int[] probeIndices = new int[childCount];
        ProbeNode[] probeNodes = new ProbeNode[childCount];
        for (int i = 0; i < childCount; i++)
            probeIndices[i] = i;
        int nextProbeNode = 0;
        int probeTreeRoot = BuildProbeTree(probeIndices, 0, childCount - 1, 0, probePositions, probeNodes, ref nextProbeNode);

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

                                    Get3ClosestProbes(worldPos, worldNormal, probePositions, probeNodes, probeTreeRoot, out int closest0, out int closest1, out int closest2);

                                    Color c = new Color(
                                        (float)closest0 / 65535.0f,
                                        (float)closest1 / 65535.0f,
                                        (float)closest2 / 65535.0f,
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

    private struct ProbeNode
    {
        public int probeIndex;
        public int left;
        public int right;
        public int axis;
    }

    private struct ProbeSearchResults
    {
        public int count;
        public int index0;
        public int index1;
        public int index2;
        public float distance0;
        public float distance1;
        public float distance2;

        public void Reset()
        {
            count = 0;
            index0 = index1 = index2 = -1;
            distance0 = distance1 = distance2 = float.MaxValue;
        }

        public float MaxDistance
        {
            get { return count < 3 ? float.MaxValue : distance2; }
        }

        public void Add(int index, float distance)
        {
            if (distance < distance0 || (distance == distance0 && index < index0))
            {
                index2 = index1;
                distance2 = distance1;
                index1 = index0;
                distance1 = distance0;
                index0 = index;
                distance0 = distance;
            }
            else if (distance < distance1 || (distance == distance1 && index < index1))
            {
                index2 = index1;
                distance2 = distance1;
                index1 = index;
                distance1 = distance;
            }
            else if (distance < distance2 || (distance == distance2 && index < index2))
            {
                index2 = index;
                distance2 = distance;
            }

            if (count < 3)
                count++;
        }
    }

    private int BuildProbeTree(int[] indices, int start, int end, int depth, Vector3[] positions, ProbeNode[] nodes, ref int nextNode)
    {
        if (start > end)
            return -1;

        int axis = depth % 3;
        int median = (start + end) / 2;
        SelectProbeMedian(indices, start, end, median, axis, positions);

        int nodeIndex = nextNode++;
        nodes[nodeIndex].probeIndex = indices[median];
        nodes[nodeIndex].axis = axis;
        nodes[nodeIndex].left = BuildProbeTree(indices, start, median - 1, depth + 1, positions, nodes, ref nextNode);
        nodes[nodeIndex].right = BuildProbeTree(indices, median + 1, end, depth + 1, positions, nodes, ref nextNode);
        return nodeIndex;
    }

    private void SelectProbeMedian(int[] indices, int start, int end, int target, int axis, Vector3[] positions)
    {
        while (start < end)
        {
            int pivotIndex = indices[(start + end) / 2];
            int pivotPosition = start;
            for (int i = start; i <= end; i++)
            {
                if (indices[i] == pivotIndex)
                {
                    Swap(indices, i, end);
                    break;
                }
            }

            for (int i = start; i < end; i++)
            {
                if (CompareProbeIndices(indices[i], pivotIndex, axis, positions) < 0)
                {
                    Swap(indices, i, pivotPosition);
                    pivotPosition++;
                }
            }
            Swap(indices, pivotPosition, end);

            if (pivotPosition == target)
                return;
            if (target < pivotPosition)
                end = pivotPosition - 1;
            else
                start = pivotPosition + 1;
        }
    }

    private int CompareProbeIndices(int indexA, int indexB, int axis, Vector3[] positions)
    {
        float coordinateA = GetCoordinate(positions[indexA], axis);
        float coordinateB = GetCoordinate(positions[indexB], axis);
        int comparison = coordinateA.CompareTo(coordinateB);
        return comparison != 0 ? comparison : indexA.CompareTo(indexB);
    }

    private float GetCoordinate(Vector3 position, int axis)
    {
        if (axis == 0) return position.x;
        if (axis == 1) return position.y;
        return position.z;
    }

    private void Swap(int[] values, int indexA, int indexB)
    {
        int value = values[indexA];
        values[indexA] = values[indexB];
        values[indexB] = value;
    }

    private void Get3ClosestProbes(Vector3 pos, Vector3 normal, Vector3[] probePositions, ProbeNode[] nodes, int root,
        out int closest0, out int closest1, out int closest2)
    {
        ProbeSearchResults results = new ProbeSearchResults();
        results.Reset();
        SearchProbeTree(root, pos, normal, probePositions, nodes, true, ref results);

        if (results.count == 0)
        {
            results.Reset();
            SearchProbeTree(root, pos, normal, probePositions, nodes, false, ref results);
        }

        closest0 = results.index0;
        closest1 = results.count > 1 ? results.index1 : results.index0;
        closest2 = results.count > 2 ? results.index2 : closest1;
    }

    private void SearchProbeTree(int nodeIndex, Vector3 pos, Vector3 normal, Vector3[] positions, ProbeNode[] nodes,
        bool requireFrontFacing, ref ProbeSearchResults results)
    {
        if (nodeIndex < 0)
            return;

        ProbeNode node = nodes[nodeIndex];
        Vector3 offset = positions[node.probeIndex] - pos;
        float distance = offset.sqrMagnitude;
        if (!requireFrontFacing || (distance > 0f && Vector3.Dot(offset, normal) > 0f))
            results.Add(node.probeIndex, distance);

        float axisOffset = GetCoordinate(pos, node.axis) - GetCoordinate(positions[node.probeIndex], node.axis);
        int nearNode = axisOffset < 0f ? node.left : node.right;
        int farNode = axisOffset < 0f ? node.right : node.left;
        SearchProbeTree(nearNode, pos, normal, positions, nodes, requireFrontFacing, ref results);
        if (axisOffset * axisOffset <= results.MaxDistance)
            SearchProbeTree(farNode, pos, normal, positions, nodes, requireFrontFacing, ref results);
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
}
