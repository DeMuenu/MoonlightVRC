using UnityEngine;
using System.Collections.Generic;

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

    [ContextMenu("Bake Probe Positions")]
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

    [ContextMenu("Bake Closest Probes Texture Array")]
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

            textureArray.SetPixels(indexPixels, lmIndex);
        }

        textureArray.Apply();

        UnityEditor.AssetDatabase.CreateAsset(textureArray, settings.probeIndexTextureArraySavePath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"Probe index Texture2DArray successfully saved to: {settings.probeIndexTextureArraySavePath}");
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

            // 1. Directional check: Dot product > 0 (probe is in the direction of the normal)
            if (Vector3.Dot(dirNormalized, normal) <= 0f) continue;

            // 2. Raycast visibility check: ensure no geometry blocks the line of sight
            Ray ray = new Ray(pos + normal * 0.001f, dirNormalized);
            if (Physics.Raycast(ray, out RaycastHit hit, dist - 0.002f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Occluded by geometry
                continue;
            }

            validProbes.Add(i);
            validDistances.Add(dist);
        }

        // Sort valid probes by distance
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
            // Fallback: If no probes pass the normal/raycast filters, fall back to absolute closest probes
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

    [ContextMenu("Apply Lightmap Indices Automatically")]
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