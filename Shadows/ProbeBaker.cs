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
                Vector3 pos = child.localPosition; 
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
            probePositions[i] = settings.probeRootTransform.GetChild(i).localPosition;
        }

        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        if (lightmaps == null || lightmaps.Length == 0)
        {
            Debug.LogError("No lightmaps found in the scene! Please bake Unity lighting first.");
            return;
        }

        int lmCount = lightmaps.Length;
        
        // Find the maximum width and height across all lightmap pages to ensure uniformity
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

        // Create a Texture2DArray using the uniform maximum dimensions
        Texture2DArray textureArray = new Texture2DArray(maxWidth, maxHeight, lmCount, TextureFormat.RGBAFloat, false);
        textureArray.filterMode = FilterMode.Point;
        textureArray.wrapMode = TextureWrapMode.Clamp;

        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();

        for (int lmIndex = 0; lmIndex < lmCount; lmIndex++)
        {
            Texture2D lmColorTex = lightmaps[lmIndex].lightmapColor;
            if (lmColorTex == null) continue;

            int pWidth = lmColorTex.width;
            int pHeight = lmColorTex.height;
            
            // Size the pixel array to match the uniform texture array dimensions
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
                Vector2[] uv2 = mesh.uv2;
                if (uv2 == null || uv2.Length == 0) uv2 = mesh.uv;
                int[] tris = mesh.triangles;

                Matrix4x4 localToWorld = renderer.localToWorldMatrix;
                Vector4 scaleOffset = renderer.lightmapScaleOffset;

                Vector3[] worldVerts = new Vector3[vertices.Length];
                Vector2[] uvPixels = new Vector2[vertices.Length];

                for (int v = 0; v < vertices.Length; v++)
                {
                    worldVerts[v] = localToWorld.MultiplyPoint(vertices[v]);
                    Vector2 lightmapUV = new Vector2(
                        uv2[v].x * scaleOffset.x + scaleOffset.z,
                        uv2[v].y * scaleOffset.y + scaleOffset.w
                    );
                    // Scale UV pixels to match the maxWidth/maxHeight canvas
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
                                int[] closest = Get3ClosestProbes(worldPos, probePositions);

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

    private int[] Get3ClosestProbes(Vector3 pos, Vector3[] probePositions)
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
                best1 = i;     dist1 = sqrDist;
            }
            else if (sqrDist < dist2)
            {
                best3 = best2; dist3 = dist2;
                best2 = i;     dist2 = sqrDist;
            }
            else if (sqrDist < dist3)
            {
                best3 = i;     dist3 = sqrDist;
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
            // Skip objects that aren't lightmapped
            if (renderer.lightmapIndex < 0) continue;

            renderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat("_LightmapIndex", renderer.lightmapIndex);
            renderer.SetPropertyBlock(propBlock);
            count++;
        }

        Debug.Log("Successfully applied automatic lightmap indices to {count} renderers.");
    }
}